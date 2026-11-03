using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IDepartmentPlanService
{
    Task<DepartmentPlanDto> EnsureAsync(SaveDepartmentPlanRequest request, CancellationToken ct);
    Task<DepartmentPlanDto> UpdateAsync(Guid id, SaveDepartmentPlanRequest request, CancellationToken ct);
}

public class DepartmentPlanService : IDepartmentPlanService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IDepartmentAccess _access;
    private readonly IAuditService _audit;

    public DepartmentPlanService(AppDbContext db, ICurrentUser current, IDepartmentAccess access, IAuditService audit)
    {
        _db = db;
        _current = current;
        _access = access;
        _audit = audit;
    }

    public async Task<DepartmentPlanDto> EnsureAsync(SaveDepartmentPlanRequest request, CancellationToken ct)
    {
        if (request.Month is < 1 or > 12) throw new AppException("Month must be between 1 and 12.");
        var departmentId = await DepartmentForWriteAsync(request.DepartmentId, ct);
        var plan = await _db.DepartmentMonthlyPlans.Include(x => x.Department)
            .FirstOrDefaultAsync(x => x.DepartmentId == departmentId && x.Year == request.Year && x.Month == request.Month, ct);
        if (plan is null)
        {
            var now = DateTime.UtcNow;
            plan = new DepartmentMonthlyPlan
            {
                Id = Guid.NewGuid(),
                DepartmentId = departmentId,
                Year = request.Year,
                Month = request.Month,
                Status = DepartmentPlanStatuses.Open,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.DepartmentMonthlyPlans.Add(plan);
            _audit.Stage(AuditActions.Created, "DepartmentMonthlyPlan", plan.Id, $"Opened {request.Year}-{request.Month:00}");
            await _db.SaveChangesAsync(ct);
            plan.Department = await _db.Departments.FirstAsync(x => x.Id == departmentId, ct);
        }

        return Map(plan);
    }

    public async Task<DepartmentPlanDto> UpdateAsync(Guid id, SaveDepartmentPlanRequest request, CancellationToken ct)
    {
        if (!DepartmentPlanStatuses.All.Contains(request.Status))
            throw new AppException("Choose a valid department plan status.");

        var plan = await _db.DepartmentMonthlyPlans.Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Department plan was not found.", StatusCodes.Status404NotFound);
        await DepartmentForWriteAsync(plan.DepartmentId, ct);
        plan.Status = request.Status;
        plan.Summary = WorkMetrics.Clean(request.Summary);
        plan.Risks = WorkMetrics.Clean(request.Risks);
        plan.Comments = WorkMetrics.Clean(request.Comments);
        plan.UpdatedAt = DateTime.UtcNow;
        _audit.Stage(AuditActions.Updated, "DepartmentMonthlyPlan", plan.Id, $"Updated {plan.Department.Name} {plan.Year}-{plan.Month:00}");
        await _db.SaveChangesAsync(ct);
        return Map(plan);
    }

    private async Task<Guid> DepartmentForWriteAsync(Guid? requested, CancellationToken ct)
    {
        var role = (await _db.Users.AsNoTracking().Where(x => x.Id == _current.UserId).Select(x => x.Role).FirstOrDefaultAsync(ct)) ?? "";
        if (!DepartmentRules.CanEditDepartmentWork(role))
            throw new AppException("The CEO can review department plans but cannot change them.", StatusCodes.Status403Forbidden);
        if (role == Roles.Admin)
        {
            if (requested is null || requested == Guid.Empty)
                throw new AppException("Choose a department.");
            if (!await _db.Departments.AnyAsync(x => x.Id == requested, ct))
                throw new AppException("Department was not found.", StatusCodes.Status404NotFound);
            return requested.Value;
        }

        _current.EnsureLead();
        return await _access.MyDepartmentAsync(ct) ?? throw new AppException("Your account is not in a department.");
    }

    private static DepartmentPlanDto Map(DepartmentMonthlyPlan plan) => new()
    {
        Id = plan.Id,
        DepartmentId = plan.DepartmentId,
        DepartmentName = plan.Department?.Name ?? "",
        Year = plan.Year,
        Month = plan.Month,
        Status = plan.Status,
        Summary = plan.Summary,
        Risks = plan.Risks,
        Comments = plan.Comments
    };
}
