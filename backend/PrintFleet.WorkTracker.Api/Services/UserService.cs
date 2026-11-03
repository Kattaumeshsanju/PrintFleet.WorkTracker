using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> ListAsync(bool includeInactive, CancellationToken ct);
    Task<UserDto> CreateAsync(SaveUserRequest request, CancellationToken ct);
    Task<UserDto> UpdateAsync(Guid id, SaveUserRequest request, CancellationToken ct);
    Task DeactivateAsync(Guid id, CancellationToken ct);
    Task<UserDto> SetOrganizationRoleAsync(Guid id, string role, Guid? departmentId, CancellationToken ct);
    Task<EmployeeOverviewDto> OverviewAsync(Guid id, int year, int month, CancellationToken ct);
}

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditService _audit;
    private readonly IMonthlyWorkService _monthly;
    private readonly IDailyWorkService _daily;
    private readonly IDepartmentAccess _access;

    public UserService(AppDbContext db, ICurrentUser current, IAuditService audit, IMonthlyWorkService monthly, IDailyWorkService daily, IDepartmentAccess access)
    {
        _db = db;
        _current = current;
        _audit = audit;
        _monthly = monthly;
        _daily = daily;
        _access = access;
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        if (!await _access.IsOrganizationViewerAsync(ct)) _current.EnsureLead();
        var query = _db.Users.AsNoTracking().Include(x => x.Department).AsQueryable();
        query = await _access.ScopeUsersAsync(query, null, ct);
        if (!includeInactive) query = query.Where(x => x.IsActive);
        var users = await query.OrderBy(x => x.Name).ToListAsync(ct);
        return users.Select(UserMapper.ToDto).ToList();
    }

    public async Task<UserDto> CreateAsync(SaveUserRequest request, CancellationToken ct)
    {
        _current.EnsureLead();
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new AppException("A password is required for a new employee.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(x => x.Email == email, ct))
            throw new AppException("An employee with that email already exists.", StatusCodes.Status409Conflict);

        var departmentId = await _access.DepartmentForUserAsync(request.DepartmentId, request.Role, ct);
        await EnsureSingleLeadAsync(request.Role, departmentId, null, ct);
        var user = new User
        {
            Id = Guid.NewGuid(),
            DepartmentId = departmentId,
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            JobTitle = WorkMetrics.Clean(request.JobTitle),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        _audit.Stage(AuditActions.Created, "User", user.Id, $"Added {user.Name} ({user.Role})");
        await _db.SaveChangesAsync(ct);
        return UserMapper.ToDto(user);
    }

    public async Task<UserDto> UpdateAsync(Guid id, SaveUserRequest request, CancellationToken ct)
    {
        _current.EnsureLead();
        var user = await _db.Users.Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);
        await _access.EnsureCanManageUserAsync(user.Id, ct);
        if (user.Role == Roles.Admin && !await _access.IsAdminAsync(ct))
            throw new AppException("Only the admin can change the admin account.", StatusCodes.Status403Forbidden);

        if (request.Role == Roles.Ceo)
            throw new AppException("Use the organization role action to grant CEO access.");
        if (user.Role == Roles.Ceo && request.Role != Roles.Ceo && !await _access.IsAdminAsync(ct))
            throw new AppException("Only the admin can change the CEO account.", StatusCodes.Status403Forbidden);
        if (user.Id == _current.UserId && request.Role != user.Role)
            throw new AppException("You cannot change your own role.");
        if (user.Id == _current.UserId && !request.IsActive)
            throw new AppException("You cannot deactivate your own account.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(x => x.Email == email && x.Id != id, ct))
            throw new AppException("An employee with that email already exists.", StatusCodes.Status409Conflict);

        var keepOrganizationRole = (user.Role is Roles.Admin or Roles.Ceo) && request.Role == user.Role;
        var departmentId = keepOrganizationRole
            ? null
            : await _access.DepartmentForUserAsync(request.DepartmentId, request.Role, ct);
        await EnsureSingleLeadAsync(request.Role, departmentId, user.Id, ct);
        user.Name = request.Name.Trim();
        user.Email = email;
        user.JobTitle = WorkMetrics.Clean(request.JobTitle);
        user.Role = request.Role;
        user.DepartmentId = departmentId;
        user.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        _audit.Stage(AuditActions.Updated, "User", user.Id, $"Updated {user.Name}");
        await _db.SaveChangesAsync(ct);
        return UserMapper.ToDto(user);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        _current.EnsureLead();
        if (id == _current.UserId)
            throw new AppException("You cannot remove your own account.");

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);
        await _access.EnsureCanManageUserAsync(user.Id, ct);
        if (user.Role is Roles.Admin or Roles.Ceo)
            throw new AppException("That organization account cannot be removed here.");

        user.IsActive = false;
        _audit.Stage(AuditActions.Updated, "User", user.Id, $"Deactivated {user.Name}");
        await _db.SaveChangesAsync(ct);
    }

    public async Task<UserDto> SetOrganizationRoleAsync(Guid id, string role, Guid? departmentId, CancellationToken ct)
    {
        if (!await _access.IsAdminAsync(ct))
            throw new AppException("Only the admin can assign organization access.", StatusCodes.Status403Forbidden);
        if (id == _current.UserId)
            throw new AppException("You cannot change your own role.");
        if (role is not (Roles.Ceo or Roles.TeamMember))
            throw new AppException("Choose CEO or Employee.");

        var user = await _db.Users.Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);
        if (user.Role == Roles.Admin)
            throw new AppException("The admin account stays with the admin.");

        if (role == Roles.Ceo)
        {
            user.Role = Roles.Ceo;
            user.DepartmentId = null;
            user.Department = null;
        }
        else
        {
            if (departmentId is null || departmentId == Guid.Empty || !await _db.Departments.AnyAsync(x => x.Id == departmentId && x.IsActive, ct))
                throw new AppException("Choose an active department for the employee.");
            user.Role = Roles.TeamMember;
            user.DepartmentId = departmentId;
        }

        _audit.Stage(AuditActions.Updated, "User", user.Id, role == Roles.Ceo ? $"Granted CEO access to {user.Name}" : $"Returned {user.Name} to a department");
        await _db.SaveChangesAsync(ct);
        return UserMapper.ToDto(user);
    }

    public async Task<EmployeeOverviewDto> OverviewAsync(Guid id, int year, int month, CancellationToken ct)
    {
        if (id != _current.UserId && !await _access.IsOrganizationViewerAsync(ct))
            await _access.EnsureCanManageUserAsync(id, ct);
        var seeDaily = id == _current.UserId || await _access.IsOrganizationViewerAsync(ct);
        var user = await _db.Users.AsNoTracking().Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);

        var sheet = await _monthly.FindSheetAsync(id, year, month, ct);
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        var calendar = await _daily.GetCalendarAsync(year, month, id, ct);
        var entries = seeDaily ? await _db.DailyWorkEntries.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.MonthlyTask).ThenInclude(x => x.Project)
            .Where(x => x.UserId == id && x.WorkDate >= start && x.WorkDate < end)
            .OrderByDescending(x => x.WorkDate).ThenBy(x => x.StartTime)
            .Take(40)
            .ToListAsync(ct) : [];

        var tasks = sheet?.Tasks ?? [];
        var remarks = tasks.Select(x => x.Remarks)
            .Concat(entries.Select(x => x.Remarks))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct()
            .Take(12)
            .ToList();

        var counts = CountTasks(tasks);
        return new EmployeeOverviewDto
        {
            User = UserMapper.ToDto(user),
            Year = year,
            Month = month,
            SheetStatus = sheet?.Status ?? SheetStatuses.Open,
            SheetId = sheet?.Id,
            PlannedTasks = counts.Total,
            Completed = counts.Completed,
            InProgress = counts.InProgress,
            Blocked = counts.Blocked,
            Cancelled = counts.Cancelled,
            Planned = counts.Planned,
            PlannedHours = counts.PlannedHours,
            ActualHours = seeDaily ? (sheet?.ActualHours ?? 0) : 0,
            CompletionPercent = counts.Completion,
            Tasks = tasks,
            RecentEntries = entries.Select(DailyWorkService.MapEntry).ToList(),
            HoursByDay = calendar.Days,
            CompletedWork = tasks.Where(x => x.Status == TaskStatuses.Completed).ToList(),
            BlockedWork = tasks.Where(x => x.Status == TaskStatuses.Blocked).ToList(),
            Remarks = remarks
        };
    }

    private async Task EnsureSingleLeadAsync(string role, Guid? departmentId, Guid? exceptUserId, CancellationToken ct)
    {
        if (role != Roles.TeamLead || departmentId is null) return;
        var taken = await _db.Users.AnyAsync(x => x.Role == Roles.TeamLead && x.IsActive && x.DepartmentId == departmentId && x.Id != exceptUserId, ct);
        if (taken)
            throw new AppException("That department already has a team lead.");
    }

    public static TaskCounts CountTasks(IEnumerable<MonthlyTaskDto> tasks)
    {
        var list = tasks.ToList();
        var counted = list.Where(x => ExecutiveMetrics.CountsTowardPlan(x.Status)).ToList();
        var completed = counted.Count(x => x.Status == TaskStatuses.Completed);
        return new TaskCounts(
            counted.Count,
            completed,
            counted.Count(x => x.Status == TaskStatuses.InProgress),
            counted.Count(x => x.Status == TaskStatuses.Blocked),
            list.Count(x => x.Status == TaskStatuses.Cancelled),
            counted.Count(x => x.Status == TaskStatuses.Planned),
            counted.Sum(x => x.EstimatedHours),
            WorkRecords.PlanCompletion(list.Select(x => (x.Status, x.ProgressPercent, x.EstimatedHours))) ?? 0);
    }
}

public readonly record struct TaskCounts(
    int Total,
    int Completed,
    int InProgress,
    int Blocked,
    int Cancelled,
    int Planned,
    decimal PlannedHours,
    decimal Completion);
