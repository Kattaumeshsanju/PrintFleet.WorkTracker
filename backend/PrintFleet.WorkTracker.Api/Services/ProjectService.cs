using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectDto>> ListAsync(string? search, string? status, Guid? departmentId, CancellationToken ct);
    Task<ProjectDto> CreateAsync(SaveProjectRequest request, CancellationToken ct);
    Task<ProjectDto> UpdateAsync(Guid id, SaveProjectRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public class ProjectService : IProjectService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditService _audit;
    private readonly IDepartmentAccess _access;
    private readonly IWorkCategoryService _categories;

    public ProjectService(AppDbContext db, ICurrentUser current, IAuditService audit, IDepartmentAccess access, IWorkCategoryService categories)
    {
        _db = db;
        _current = current;
        _audit = audit;
        _access = access;
        _categories = categories;
    }

    public async Task<IReadOnlyList<ProjectDto>> ListAsync(string? search, string? status, Guid? departmentId, CancellationToken ct)
    {
        var query = _db.Projects.AsNoTracking().Include(x => x.Department).Include(x => x.Owner).Include(x => x.WorkCategory).AsQueryable();
        if (await _access.IsOrganizationViewerAsync(ct))
        {
            if (departmentId is Guid id && id != Guid.Empty)
                query = query.Where(x => x.DepartmentId == id);
        }
        else
        {
            var mine = await _access.MyDepartmentAsync(ct);
            query = query.Where(x => x.DepartmentId == mine);
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term) || (x.Description != null && x.Description.ToLower().Contains(term)));
        }

        var projects = await query.OrderBy(x => x.Name).ToListAsync(ct);
        return projects.Select(Map).ToList();
    }

    public async Task<ProjectDto> CreateAsync(SaveProjectRequest request, CancellationToken ct)
    {
        _current.EnsureLead();
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.Projects.AnyAsync(x => x.Code == code, ct))
            throw new AppException("A project with that code already exists.", StatusCodes.Status409Conflict);

        var departmentId = await _access.DepartmentForProjectAsync(request.DepartmentId, ct);
        var now = DateTime.UtcNow;
        var project = new Project
        {
            Id = Guid.NewGuid(),
            DepartmentId = departmentId,
            OwnerId = await OwnerForAsync(request.OwnerId, departmentId, ct),
            Name = request.Name.Trim(),
            Code = code,
            Priority = request.Priority,
            Description = WorkMetrics.Clean(request.Description),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = request.Status,
            WorkCategoryId = await _categories.AssignAsync(request.WorkCategoryId, departmentId, null, ct),
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Projects.Add(project);
        _audit.Stage(AuditActions.Created, "Project", project.Id, $"Created project {project.Code} {project.Name}");
        await _db.SaveChangesAsync(ct);
        return Map(project);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, SaveProjectRequest request, CancellationToken ct)
    {
        _current.EnsureLead();
        var project = await _db.Projects.Include(x => x.Department).Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Project was not found.", StatusCodes.Status404NotFound);
        if (!await _access.IsAdminAsync(ct) && project.DepartmentId != await _access.MyDepartmentAsync(ct))
            throw new AppException("That project belongs to another department.", StatusCodes.Status403Forbidden);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.Projects.AnyAsync(x => x.Code == code && x.Id != id, ct))
            throw new AppException("A project with that code already exists.", StatusCodes.Status409Conflict);

        project.Name = request.Name.Trim();
        project.Code = code;
        project.Priority = request.Priority;
        project.Description = WorkMetrics.Clean(request.Description);
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;
        project.Status = request.Status;
        if (await _access.IsAdminAsync(ct) && request.DepartmentId is Guid departmentId && departmentId != Guid.Empty && departmentId != project.DepartmentId)
        {
            var hasGoals = await _db.MonthlyTasks.AnyAsync(x => x.ProjectId == project.Id, ct);
            if (!WorkRecords.CanChangeProjectDepartment(hasGoals))
                throw new AppException("This project already has monthly goals, so its department stays with those records.");
            project.DepartmentId = await _access.DepartmentForProjectAsync(departmentId, ct);
            if (project.WorkCategoryId is not null && request.WorkCategoryId == project.WorkCategoryId)
                project.WorkCategoryId = null;
        }
        project.WorkCategoryId = await _categories.AssignAsync(request.WorkCategoryId, project.DepartmentId ?? Guid.Empty, project.WorkCategoryId, ct);
        project.OwnerId = await OwnerForAsync(request.OwnerId, project.DepartmentId ?? Guid.Empty, ct);
        project.UpdatedAt = DateTime.UtcNow;
        _audit.Stage(AuditActions.Updated, "Project", project.Id, $"Updated project {project.Code}");
        await _db.SaveChangesAsync(ct);
        return Map(project);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        _current.EnsureLead();
        var project = await _db.Projects.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Project was not found.", StatusCodes.Status404NotFound);
        if (!await _access.IsAdminAsync(ct) && project.DepartmentId != await _access.MyDepartmentAsync(ct))
            throw new AppException("That project belongs to another department.", StatusCodes.Status403Forbidden);

        if (await _db.MonthlyTasks.AnyAsync(x => x.ProjectId == id, ct))
            throw new AppException("This project is used by monthly tasks. Set its status to Cancelled instead of deleting it.");
        if (await _db.Initiatives.AnyAsync(x => x.ProjectId == id, ct))
            throw new AppException("This project is linked to an initiative. Remove that link instead of deleting the project.");

        _db.Projects.Remove(project);
        _audit.Stage(AuditActions.Deleted, "Project", project.Id, $"Deleted project {project.Code}");
        await _db.SaveChangesAsync(ct);
    }

    public static ProjectDto Map(Project project) => new()
    {
        Id = project.Id,
        Name = project.Name,
        Code = project.Code,
        Description = project.Description,
        StartDate = project.StartDate,
        EndDate = project.EndDate,
        Status = project.Status,
        Priority = project.Priority,
        OwnerId = project.OwnerId,
        OwnerName = project.Owner?.Name,
        DepartmentId = project.DepartmentId,
        DepartmentName = project.Department?.Name,
        WorkCategoryId = project.WorkCategoryId,
        WorkCategoryName = project.WorkCategory?.Name,
        CreatedAt = project.CreatedAt
    };

    private async Task<Guid?> OwnerForAsync(Guid? ownerId, Guid departmentId, CancellationToken ct)
    {
        if (ownerId is null || ownerId == Guid.Empty) return null;
        var owner = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == ownerId, ct)
            ?? throw new AppException("Project owner was not found.", StatusCodes.Status404NotFound);
        if (!owner.IsActive || owner.DepartmentId != departmentId || owner.Role is Roles.Admin or Roles.Ceo)
            throw new AppException("The project owner must be an active person in this department.");
        return owner.Id;
    }
}
