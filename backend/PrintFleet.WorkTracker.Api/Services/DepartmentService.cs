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
    Task<DepartmentDto> RenameAsync(Guid id, SaveDepartmentRequest request, CancellationToken ct);
    Task<DepartmentDto> SetStatusAsync(Guid id, bool isActive, CancellationToken ct);
    Task<DepartmentDto> AssignMemberAsync(Guid id, AssignDepartmentMemberRequest request, CancellationToken ct);
    Task<DepartmentDto> SaveTeamAsync(Guid id, SaveDepartmentTeamRequest request, CancellationToken ct);
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
        var across = await _access.CanViewOrganizationOverviewAsync(ct) || await _access.CanViewOrganizationReportsAsync(ct);
        if (!across) _current.EnsureLead();
        var query = _db.Departments.AsNoTracking().AsQueryable();
        if (!across)
        {
            var mine = await _access.MyDepartmentAsync(ct);
            query = query.Where(x => x.Id == mine);
        }

        var departments = await query.OrderBy(x => x.Name).ToListAsync(ct);
        var ids = departments.Select(x => x.Id).ToArray();
        var people = await _db.Users.AsNoTracking()
            .Where(x => x.IsActive && x.DepartmentId != null && ids.Contains(x.DepartmentId.Value))
            .Select(x => new { x.DepartmentId, x.Id, x.Name, x.Role })
            .ToListAsync(ct);
        var projects = await _db.Projects.AsNoTracking()
            .Where(x => x.DepartmentId != null && ids.Contains(x.DepartmentId.Value) && x.Status == ProjectStatuses.Active)
            .GroupBy(x => x.DepartmentId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return departments.Select(department =>
        {
            var members = people.Where(x => x.DepartmentId == department.Id).ToList();
            var lead = members.FirstOrDefault(x => x.Role == Roles.TeamLead);
            return new DepartmentDto
            {
                Id = department.Id,
                Name = department.Name,
                IsActive = department.IsActive,
                TeamLeadId = lead?.Id,
                TeamLeadName = lead?.Name,
                MemberCount = members.Count,
                ActiveProjectCount = projects.FirstOrDefault(x => x.Id == department.Id)?.Count ?? 0
            };
        }).ToList();
    }

    public async Task<DepartmentDto> CreateAsync(SaveDepartmentRequest request, CancellationToken ct)
    {
        if (!await _access.IsAdminAsync(ct))
            throw new AppException("Only the admin can add a department.", StatusCodes.Status403Forbidden);

        var name = request.Name.Trim();
        await EnsureUniqueNameAsync(name, null, ct);
        var department = new Department
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Departments.Add(department);
        _audit.Stage(AuditActions.Created, "Department", department.Id, $"Added department {department.Name}");
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(department, ct);
    }

    public async Task<DepartmentDto> RenameAsync(Guid id, SaveDepartmentRequest request, CancellationToken ct)
    {
        if (!await _access.IsAdminAsync(ct))
            throw new AppException("Only the admin can rename a department.", StatusCodes.Status403Forbidden);

        var department = await FindAsync(id, ct);
        var name = request.Name.Trim();
        await EnsureUniqueNameAsync(name, department.Id, ct);
        department.Name = name;
        _audit.Stage(AuditActions.Updated, "Department", department.Id, $"Renamed department to {department.Name}");
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(department, ct);
    }

    public async Task<DepartmentDto> SetStatusAsync(Guid id, bool isActive, CancellationToken ct)
    {
        if (!await _access.IsAdminAsync(ct))
            throw new AppException("Only the admin can change a department.", StatusCodes.Status403Forbidden);

        var department = await FindAsync(id, ct);
        if (!isActive && department.IsActive)
        {
            var activeUsers = await _db.Users.CountAsync(x => x.DepartmentId == id && x.IsActive, ct);
            var activeProjects = await _db.Projects.CountAsync(x => x.DepartmentId == id && x.Status == ProjectStatuses.Active, ct);
            if (!DepartmentRules.CanDeactivateDepartment(activeUsers, activeProjects))
                throw new AppException("Move active people and projects out of this department before deactivating it.");
        }

        department.IsActive = isActive;
        _audit.Stage(AuditActions.Updated, "Department", department.Id, isActive ? $"Reactivated {department.Name}" : $"Deactivated {department.Name}");
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(department, ct);
    }

    public async Task<DepartmentDto> AssignMemberAsync(Guid id, AssignDepartmentMemberRequest request, CancellationToken ct)
    {
        var department = await FindAsync(id, ct);
        if (!department.IsActive)
            throw new AppException("That department is inactive.");

        var isAdmin = await _access.IsAdminAsync(ct);
        if (!DepartmentRules.CanAssignRole(isAdmin, request.Role))
            throw new AppException("Only the admin can assign a team lead.", StatusCodes.Status403Forbidden);
        if (!isAdmin) _current.EnsureLead();

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == request.UserId, ct)
            ?? throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);
        if (user.Role is Roles.Admin or Roles.Ceo)
            throw new AppException("Organization accounts are not assigned to a department.");

        var mine = await _access.MyDepartmentAsync(ct);
        if (!DepartmentRules.CanMoveIntoDepartment(isAdmin, mine, user.DepartmentId, department.Id))
            throw new AppException("You can manage people only in your own department.", StatusCodes.Status403Forbidden);

        if (request.Role == Roles.TeamLead)
        {
            var currentLeads = await _db.Users
                .Where(x => x.DepartmentId == department.Id && x.Role == Roles.TeamLead && x.IsActive && x.Id != user.Id)
                .ToListAsync(ct);
            foreach (var lead in currentLeads)
                lead.Role = Roles.TeamMember;
            user.Role = Roles.TeamLead;
        }
        else
        {
            user.Role = Roles.TeamMember;
        }

        user.DepartmentId = department.Id;
        _audit.Stage(AuditActions.Updated, "User", user.Id, $"Assigned {user.Name} to {department.Name} as {user.Role}");
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await ToDtoAsync(department, ct);
    }

    public async Task<DepartmentDto> SaveTeamAsync(Guid id, SaveDepartmentTeamRequest request, CancellationToken ct)
    {
        var department = await FindAsync(id, ct);
        if (!department.IsActive)
            throw new AppException("That department is inactive.");

        var isAdmin = await _access.IsAdminAsync(ct);
        if (!isAdmin) _current.EnsureLead();

        var members = request.Members ?? [];
        var newcomers = request.NewMembers ?? [];
        if (members.Select(x => x.UserId).Distinct().Count() != members.Count)
            throw new AppException("Each person can appear only once.");

        var roles = members.Select(x => x.Role).Concat(newcomers.Select(x => x.Role)).ToList();
        if (roles.Any(role => role is not (Roles.TeamLead or Roles.TeamMember)))
            throw new AppException("Choose Team Lead or Employee.");
        if (!DepartmentRules.HasSingleOrNoLead(roles))
            throw new AppException("A department can have only one team lead.");

        var emails = newcomers.Select(x => x.Email.Trim().ToLowerInvariant()).ToList();
        if (emails.Distinct().Count() != emails.Count || (emails.Count > 0 && await _db.Users.AnyAsync(x => emails.Contains(x.Email), ct)))
            throw new AppException("An employee with that email already exists.", StatusCodes.Status409Conflict);

        var requestedIds = members.Select(x => x.UserId).ToList();
        var requestedLead = members.FirstOrDefault(x => x.Role == Roles.TeamLead);
        if (newcomers.Any(x => x.Role == Roles.TeamLead) && requestedLead is not null)
            throw new AppException("A department can have only one team lead.");

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var currentMembers = await _db.Users.Where(x => x.DepartmentId == id).ToListAsync(ct);
        var currentLeadId = currentMembers.Where(x => x.IsActive && x.Role == Roles.TeamLead).Select(x => (Guid?)x.Id).FirstOrDefault();
        var requestedLeadId = newcomers.Any(x => x.Role == Roles.TeamLead) ? Guid.NewGuid() : requestedLead?.UserId;
        if (!DepartmentRules.CanChangeTeamLead(isAdmin, currentLeadId, requestedLeadId))
            throw new AppException("Only the admin can change the team lead.", StatusCodes.Status403Forbidden);

        var people = requestedIds.Count == 0
            ? []
            : await _db.Users.Where(x => requestedIds.Contains(x.Id)).ToListAsync(ct);
        if (people.Count != requestedIds.Count)
            throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);

        var mine = await _access.MyDepartmentAsync(ct);
        foreach (var person in people)
        {
            if (person.Role is Roles.Admin or Roles.Ceo)
                throw new AppException("Organization accounts are not assigned to a department.");
            var assignment = members.First(x => x.UserId == person.Id);
            if (assignment.Role == Roles.TeamLead && !person.IsActive)
                throw new AppException("The team lead must be an active account.");
            if (!DepartmentRules.CanMoveIntoDepartment(isAdmin, mine, person.DepartmentId, department.Id))
                throw new AppException("You can manage people only in your own department.", StatusCodes.Status403Forbidden);
        }

        if (!isAdmin && newcomers.Any(x => x.Role != Roles.TeamMember))
            throw new AppException("Only the admin can assign a team lead.", StatusCodes.Status403Forbidden);

        foreach (var person in currentMembers.Where(x => !requestedIds.Contains(x.Id)))
        {
            if (person.Id == _current.UserId)
                throw new AppException("You cannot remove yourself from this department.");
            if (person.Role is Roles.Admin or Roles.Ceo)
                throw new AppException("Organization accounts are not assigned to a department.");
            person.DepartmentId = null;
            if (person.Role == Roles.TeamLead) person.Role = Roles.TeamMember;
            _audit.Stage(AuditActions.Updated, "User", person.Id, $"Removed {person.Name} from {department.Name}");
        }

        foreach (var person in people)
        {
            var assignment = members.First(x => x.UserId == person.Id);
            var moved = person.DepartmentId != department.Id;
            person.DepartmentId = department.Id;
            person.Role = assignment.Role;
            _audit.Stage(AuditActions.Updated, "User", person.Id, moved
                ? $"Moved {person.Name} to {department.Name} as {person.Role}"
                : $"Updated {person.Name} in {department.Name} as {person.Role}");
        }

        foreach (var newcomer in newcomers)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                DepartmentId = department.Id,
                Name = newcomer.Name.Trim(),
                Email = newcomer.Email.Trim().ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(newcomer.Password),
                Role = newcomer.Role,
                JobTitle = WorkMetrics.Clean(newcomer.JobTitle),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(user);
            _audit.Stage(AuditActions.Created, "User", user.Id, $"Added {user.Name} to {department.Name} as {user.Role}");
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await ToDtoAsync(department, ct);
    }

    private async Task<Department> FindAsync(Guid id, CancellationToken ct)
        => await _db.Departments.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Department was not found.", StatusCodes.Status404NotFound);

    private async Task EnsureUniqueNameAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var taken = await _db.Departments.AnyAsync(x => x.Name.ToLower() == name.ToLower() && x.Id != exceptId, ct);
        if (taken) throw new AppException("That department already exists.", StatusCodes.Status409Conflict);
    }

    private async Task<DepartmentDto> ToDtoAsync(Department department, CancellationToken ct)
    {
        var members = await _db.Users.AsNoTracking()
            .Where(x => x.IsActive && x.DepartmentId == department.Id)
            .Select(x => new { x.Id, x.Name, x.Role })
            .ToListAsync(ct);
        var lead = members.FirstOrDefault(x => x.Role == Roles.TeamLead);
        var projects = await _db.Projects.CountAsync(x => x.DepartmentId == department.Id && x.Status == ProjectStatuses.Active, ct);
        return new DepartmentDto
        {
            Id = department.Id,
            Name = department.Name,
            IsActive = department.IsActive,
            TeamLeadId = lead?.Id,
            TeamLeadName = lead?.Name,
            MemberCount = members.Count,
            ActiveProjectCount = projects
        };
    }
}
