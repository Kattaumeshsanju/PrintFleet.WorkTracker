using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IDepartmentService
{
    Task<IReadOnlyList<DepartmentDto>> ListAsync(CancellationToken ct);
    Task<DepartmentDto> CreateAsync(SaveDepartmentRequest request, CancellationToken ct);
}

public class DepartmentService : IDepartmentService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IDepartmentAccess _access;
    private readonly IAuditService _audit;

    public DepartmentService(AppDbContext db, ICurrentUser current, IDepartmentAccess access, IAuditService audit)
    {
        _db = db;
        _current = current;
        _access = access;
        _audit = audit;
    }

    public async Task<IReadOnlyList<DepartmentDto>> ListAsync(CancellationToken ct)
    {
        _current.EnsureLead();
        var query = _db.Departments.AsNoTracking().AsQueryable();
        if (!await _access.IsAdminAsync(ct))
        {
            var mine = await _access.MyDepartmentAsync(ct);
            query = query.Where(x => x.Id == mine);
        }

        return await query.OrderBy(x => x.Name)
            .Select(x => new DepartmentDto { Id = x.Id, Name = x.Name })
            .ToListAsync(ct);
    }

    public async Task<DepartmentDto> CreateAsync(SaveDepartmentRequest request, CancellationToken ct)
    {
        if (!await _access.IsAdminAsync(ct))
            throw new AppException("Only the admin can add a department.", StatusCodes.Status403Forbidden);

        var name = request.Name.Trim();
        if (await _db.Departments.AnyAsync(x => x.Name.ToLower() == name.ToLower(), ct))
            throw new AppException("That department already exists.", StatusCodes.Status409Conflict);

        var department = new Department
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTime.UtcNow
        };
        _db.Departments.Add(department);
        _audit.Stage(AuditActions.Created, "Department", department.Id, $"Added department {department.Name}");
        await _db.SaveChangesAsync(ct);
        return new DepartmentDto { Id = department.Id, Name = department.Name };
    }
}
