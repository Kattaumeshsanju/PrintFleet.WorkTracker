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
    Task<EmployeeOverviewDto> OverviewAsync(Guid id, int year, int month, CancellationToken ct);
}

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditService _audit;
    private readonly IMonthlyWorkService _monthly;
    private readonly IDailyWorkService _daily;

    public UserService(AppDbContext db, ICurrentUser current, IAuditService audit, IMonthlyWorkService monthly, IDailyWorkService daily)
    {
        _db = db;
        _current = current;
        _audit = audit;
        _monthly = monthly;
        _daily = daily;
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        _current.EnsureLead();
        var query = _db.Users.AsNoTracking().AsQueryable();
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

        var user = new User
        {
            Id = Guid.NewGuid(),
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
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);

        if (user.Id == _current.UserId && request.Role != user.Role)
            throw new AppException("You cannot change your own role.");
        if (user.Id == _current.UserId && !request.IsActive)
            throw new AppException("You cannot deactivate your own account.");

        if (user.Role == Roles.TeamLead && request.Role != Roles.TeamLead)
        {
            var otherLeads = await _db.Users.CountAsync(x => x.Role == Roles.TeamLead && x.IsActive && x.Id != user.Id, ct);
            if (otherLeads == 0)
                throw new AppException("The team needs at least one active Team Lead.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(x => x.Email == email && x.Id != id, ct))
            throw new AppException("An employee with that email already exists.", StatusCodes.Status409Conflict);

        user.Name = request.Name.Trim();
        user.Email = email;
        user.JobTitle = WorkMetrics.Clean(request.JobTitle);
        user.Role = request.Role;
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

        if (user.Role == Roles.TeamLead)
        {
            var otherLeads = await _db.Users.CountAsync(x => x.Role == Roles.TeamLead && x.IsActive && x.Id != user.Id, ct);
            if (otherLeads == 0)
                throw new AppException("The team needs at least one active Team Lead.");
        }

        user.IsActive = false;
        _audit.Stage(AuditActions.Updated, "User", user.Id, $"Deactivated {user.Name}");
        await _db.SaveChangesAsync(ct);
    }

    public async Task<EmployeeOverviewDto> OverviewAsync(Guid id, int year, int month, CancellationToken ct)
    {
        if (id != _current.UserId) _current.EnsureLead();
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);

        var sheet = await _monthly.FindSheetAsync(id, year, month, ct);
        var calendar = await _daily.GetCalendarAsync(year, month, id, ct);
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        var entries = await _db.DailyWorkEntries.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.MonthlyTask).ThenInclude(x => x.Project)
            .Where(x => x.UserId == id && x.WorkDate >= start && x.WorkDate < end)
            .OrderByDescending(x => x.WorkDate).ThenBy(x => x.StartTime)
            .Take(40)
            .ToListAsync(ct);

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
            ActualHours = sheet?.ActualHours ?? 0,
            CompletionPercent = counts.Completion,
            Tasks = tasks,
            RecentEntries = entries.Select(DailyWorkService.MapEntry).ToList(),
            HoursByDay = calendar.Days,
            CompletedWork = tasks.Where(x => x.Status == TaskStatuses.Completed).ToList(),
            BlockedWork = tasks.Where(x => x.Status == TaskStatuses.Blocked).ToList(),
            Remarks = remarks
        };
    }

    public static TaskCounts CountTasks(IEnumerable<MonthlyTaskDto> tasks)
    {
        var list = tasks.ToList();
        var completed = list.Count(x => x.Status == TaskStatuses.Completed);
        return new TaskCounts(
            list.Count,
            completed,
            list.Count(x => x.Status == TaskStatuses.InProgress),
            list.Count(x => x.Status == TaskStatuses.Blocked),
            list.Count(x => x.Status == TaskStatuses.Cancelled),
            list.Count(x => x.Status == TaskStatuses.Planned),
            list.Sum(x => x.EstimatedHours),
            WorkMetrics.Completion(completed, list.Count));
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
