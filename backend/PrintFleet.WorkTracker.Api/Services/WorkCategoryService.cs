using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IWorkCategoryService
{
    Task<IReadOnlyList<WorkCategoryDto>> ListAsync(Guid? departmentId, CancellationToken ct);
    Task<WorkCategoryDto> CreateAsync(SaveWorkCategoryRequest request, CancellationToken ct);
    Task<WorkCategoryDto> UpdateAsync(Guid id, SaveWorkCategoryRequest request, CancellationToken ct);
    Task DeactivateAsync(Guid id, CancellationToken ct);
    Task ReorderAsync(ReorderWorkCategoriesRequest request, CancellationToken ct);
    Task ReassignAsync(Guid id, ReassignWorkCategoryRequest request, CancellationToken ct);
    Task<Guid?> AssignAsync(Guid? categoryId, Guid departmentId, Guid? currentCategoryId, CancellationToken ct);
}

public class WorkCategoryService : IWorkCategoryService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditService _audit;

    public WorkCategoryService(AppDbContext db, ICurrentUser current, IAuditService audit)
    {
        _db = db;
        _current = current;
        _audit = audit;
    }

    public async Task<IReadOnlyList<WorkCategoryDto>> ListAsync(Guid? departmentId, CancellationToken ct)
    {
        var (me, scoped) = await ScopeAsync(departmentId, false, ct);
        var query = _db.WorkCategories.AsNoTracking().Include(x => x.Department).AsQueryable();
        if (me.Role is Roles.Admin or Roles.Ceo)
        {
            if (scoped != Guid.Empty) query = query.Where(x => x.DepartmentId == scoped);
        }
        else query = query.Where(x => x.DepartmentId == scoped);

        var rows = await query.OrderBy(x => x.Department.Name).ThenBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToListAsync(ct);
        var creators = await NamesAsync(rows.Select(x => x.CreatedById), ct);
        return rows.Select(x => Map(x, creators)).ToList();
    }

    public async Task<WorkCategoryDto> CreateAsync(SaveWorkCategoryRequest request, CancellationToken ct)
    {
        var (_, departmentId) = await ScopeAsync(request.DepartmentId, true, ct);
        var name = CategoryRules.Normalize(request.Name);
        if (name.Length == 0) throw new AppException("Enter a category name.");
        var key = CategoryRules.Key(name);
        if (await _db.WorkCategories.AnyAsync(x => x.DepartmentId == departmentId && x.IsActive && x.NameKey == key, ct))
            throw new AppException("This department already has an active category with that name.", StatusCodes.Status409Conflict);
        var order = await _db.WorkCategories.Where(x => x.DepartmentId == departmentId).Select(x => (int?)x.DisplayOrder).MaxAsync(ct) ?? 0;
        var category = new WorkCategory
        {
            Id = Guid.NewGuid(),
            DepartmentId = departmentId,
            Name = name,
            NameKey = key,
            Description = WorkMetrics.Clean(request.Description),
            IsActive = true,
            DisplayOrder = order + 1,
            CreatedById = _current.UserId,
            CreatedAt = DateTime.UtcNow
        };
        _db.WorkCategories.Add(category);
        _audit.Stage(AuditActions.Created, "WorkCategory", category.Id, $"Created category {category.Name}");
        await _db.SaveChangesAsync(ct);
        category.Department = await _db.Departments.FirstAsync(x => x.Id == departmentId, ct);
        return Map(category, new Dictionary<Guid, string> { [_current.UserId] = _current.Name });
    }

    public async Task<WorkCategoryDto> UpdateAsync(Guid id, SaveWorkCategoryRequest request, CancellationToken ct)
    {
        var category = await FindAsync(id, ct);
        await ScopeAsync(category.DepartmentId, true, ct);
        var name = CategoryRules.Normalize(request.Name);
        if (name.Length == 0) throw new AppException("Enter a category name.");
        var key = CategoryRules.Key(name);
        if (category.IsActive && await _db.WorkCategories.AnyAsync(x => x.Id != id && x.DepartmentId == category.DepartmentId && x.IsActive && x.NameKey == key, ct))
            throw new AppException("This department already has an active category with that name.", StatusCodes.Status409Conflict);
        category.Name = name;
        category.NameKey = key;
        category.Description = WorkMetrics.Clean(request.Description);
        _audit.Stage(AuditActions.Updated, "WorkCategory", category.Id, $"Renamed category to {category.Name}");
        await _db.SaveChangesAsync(ct);
        return Map(category, await NamesAsync([category.CreatedById], ct));
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        var category = await FindAsync(id, ct);
        await ScopeAsync(category.DepartmentId, true, ct);
        category.IsActive = false;
        _audit.Stage(AuditActions.Updated, "WorkCategory", category.Id, $"Deactivated category {category.Name}");
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReorderAsync(ReorderWorkCategoriesRequest request, CancellationToken ct)
    {
        var (_, departmentId) = await ScopeAsync(request.DepartmentId, true, ct);
        var rows = await _db.WorkCategories.Where(x => x.DepartmentId == departmentId).ToListAsync(ct);
        if (request.Ids.Count != rows.Count || request.Ids.Distinct().Count() != rows.Count || rows.Any(x => !request.Ids.Contains(x.Id)))
            throw new AppException("Reorder the categories that belong to this department.");
        for (var index = 0; index < request.Ids.Count; index++)
            rows.First(x => x.Id == request.Ids[index]).DisplayOrder = index + 1;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReassignAsync(Guid id, ReassignWorkCategoryRequest request, CancellationToken ct)
    {
        var source = await FindAsync(id, ct);
        var target = await FindAsync(request.TargetCategoryId, ct);
        await ScopeAsync(source.DepartmentId, true, ct);
        if (!CategoryRules.CanReassign(source.DepartmentId, target.DepartmentId))
            throw new AppException("Categories can be reassigned only inside the same department.", StatusCodes.Status403Forbidden);
        if (source.Id == target.Id) throw new AppException("Choose a different category.");
        if (!target.IsActive) throw new AppException("Choose an active category.");

        var projects = await _db.Projects.Where(x => x.WorkCategoryId == source.Id).ToListAsync(ct);
        var initiatives = await _db.Initiatives.Where(x => x.WorkCategoryId == source.Id).ToListAsync(ct);
        var tasks = await _db.MonthlyTasks.Where(x => x.WorkCategoryId == source.Id).ToListAsync(ct);
        foreach (var project in projects) project.WorkCategoryId = target.Id;
        foreach (var initiative in initiatives) initiative.WorkCategoryId = target.Id;
        foreach (var task in tasks) task.WorkCategoryId = target.Id;
        _audit.Stage(AuditActions.Updated, "WorkCategory", source.Id, $"Reassigned {projects.Count} projects, {initiatives.Count} initiatives, and {tasks.Count} goals from {source.Name} to {target.Name}");
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Guid?> AssignAsync(Guid? categoryId, Guid departmentId, Guid? currentCategoryId, CancellationToken ct)
    {
        if (categoryId is null || categoryId == Guid.Empty) return null;
        var category = await _db.WorkCategories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == categoryId, ct)
            ?? throw new AppException("Work category was not found.", StatusCodes.Status404NotFound);
        if (!CategoryRules.CanAssign(category.DepartmentId, departmentId, category.IsActive, category.Id == currentCategoryId))
            throw new AppException("Choose an active category from the same department.", StatusCodes.Status403Forbidden);
        return category.Id;
    }

    private async Task<WorkCategory> FindAsync(Guid id, CancellationToken ct)
        => await _db.WorkCategories.Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Work category was not found.", StatusCodes.Status404NotFound);

    private async Task<(User Me, Guid DepartmentId)> ScopeAsync(Guid? requested, bool mutate, CancellationToken ct)
    {
        var me = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == _current.UserId, ct)
            ?? throw new AppException("Your account was not found.", StatusCodes.Status401Unauthorized);
        if (mutate && !CategoryRules.CanManage(me.Role, me.DepartmentId, requested ?? me.DepartmentId ?? Guid.Empty))
            throw new AppException("You cannot change categories for that department.", StatusCodes.Status403Forbidden);
        if (me.Role is Roles.Admin or Roles.Ceo)
        {
            if (mutate && me.Role != Roles.Admin)
                throw new AppException("The CEO can view categories, not change them.", StatusCodes.Status403Forbidden);
            if (mutate && (requested is null || requested == Guid.Empty))
                throw new AppException("Choose a department.");
            return (me, requested is Guid id && id != Guid.Empty ? id : Guid.Empty);
        }
        if (me.DepartmentId is null)
            throw new AppException("You are not assigned to a department.", StatusCodes.Status403Forbidden);
        if (requested is Guid asked && asked != Guid.Empty && asked != me.DepartmentId)
            throw new AppException("You cannot use another department's categories.", StatusCodes.Status403Forbidden);
        return (me, me.DepartmentId.Value);
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var wanted = ids.Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        if (wanted.Count == 0) return [];
        return await _db.Users.AsNoTracking().Where(x => wanted.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
    }

    private static WorkCategoryDto Map(WorkCategory category, IReadOnlyDictionary<Guid, string> names) => new()
    {
        Id = category.Id,
        DepartmentId = category.DepartmentId,
        DepartmentName = category.Department?.Name ?? "",
        Name = category.Name,
        Description = category.Description,
        IsActive = category.IsActive,
        DisplayOrder = category.DisplayOrder,
        CreatedByName = category.CreatedById is Guid id && names.TryGetValue(id, out var name) ? name : null,
        CreatedAt = category.CreatedAt
    };
}
