using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IMonthlyWorkService
{
    Task<MonthlySheetDto> EnsureAsync(EnsureSheetRequest request, CancellationToken ct);
    Task<MonthlySheetDto?> FindSheetAsync(Guid userId, int year, int month, CancellationToken ct);
    Task<MonthlySheetDto> GetAsync(Guid id, CancellationToken ct);
    Task<TeamPlansDto> TeamAsync(int year, int month, Guid? userId, Guid? projectId, string? status, string? priority, string? search, Guid? departmentId, CancellationToken ct);
    Task<MonthlyTaskDto> AddTaskAsync(Guid sheetId, SaveTaskRequest request, CancellationToken ct);
    Task<MonthlyTaskDto> UpdateTaskAsync(Guid taskId, SaveTaskRequest request, CancellationToken ct);
    Task DeleteTaskAsync(Guid taskId, CancellationToken ct);
    Task<MonthlySheetDto> SetLockAsync(Guid sheetId, bool locked, CancellationToken ct);
}

public class MonthlyWorkService : IMonthlyWorkService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditService _audit;
    private readonly IDepartmentAccess _access;

    public MonthlyWorkService(AppDbContext db, ICurrentUser current, IAuditService audit, IDepartmentAccess access)
    {
        _db = db;
        _current = current;
        _audit = audit;
        _access = access;
    }

    public async Task<MonthlySheetDto> EnsureAsync(EnsureSheetRequest request, CancellationToken ct)
    {
        var target = request.UserId is null || request.UserId == Guid.Empty ? _current.UserId : request.UserId.Value;
        await _access.EnsureSameDepartmentAsync(target, ct);
        if (!await _db.Users.AnyAsync(x => x.Id == target && x.IsActive, ct))
            throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);

        var existing = await _db.MonthlyWorkSheets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == target && x.Year == request.Year && x.Month == request.Month, ct);
        if (existing is not null) return await GetAsync(existing.Id, ct);

        var now = DateTime.UtcNow;
        var sheet = new MonthlyWorkSheet
        {
            Id = Guid.NewGuid(),
            UserId = target,
            Year = request.Year,
            Month = request.Month,
            Status = SheetStatuses.Open,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.MonthlyWorkSheets.Add(sheet);
        _audit.Stage(AuditActions.Created, "MonthlyWorkSheet", sheet.Id, $"Opened {request.Year}-{request.Month:00} work sheet");
        await _db.SaveChangesAsync(ct);
        return await GetAsync(sheet.Id, ct);
    }

    public async Task<MonthlySheetDto?> FindSheetAsync(Guid userId, int year, int month, CancellationToken ct)
    {
        var id = await _db.MonthlyWorkSheets.AsNoTracking()
            .Where(x => x.UserId == userId && x.Year == year && x.Month == month)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);
        return id is null ? null : await GetAsync(id.Value, ct);
    }

    public async Task<MonthlySheetDto> GetAsync(Guid id, CancellationToken ct)
    {
        var sheet = await _db.MonthlyWorkSheets.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Tasks).ThenInclude(x => x.Project)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Monthly work sheet was not found.", StatusCodes.Status404NotFound);
        await _access.EnsureSameDepartmentAsync(sheet.UserId, ct);

        return await MapSheetAsync(sheet, ct);
    }

    public async Task<TeamPlansDto> TeamAsync(int year, int month, Guid? userId, Guid? projectId, string? status, string? priority, string? search, Guid? departmentId, CancellationToken ct)
    {
        if (month is < 1 or > 12) throw new AppException("Month must be between 1 and 12.");

        var usersQuery = _db.Users.AsNoTracking().Include(x => x.Department).Where(x => x.IsActive);
        usersQuery = await _access.ScopeUsersAsync(usersQuery, departmentId, ct);
        if (userId is Guid filterUser) usersQuery = usersQuery.Where(x => x.Id == filterUser);
        var users = await usersQuery.OrderBy(x => x.Name).ToListAsync(ct);
        var userIds = users.Select(x => x.Id).ToList();

        var sheets = await _db.MonthlyWorkSheets.AsNoTracking()
            .Include(x => x.Tasks).ThenInclude(x => x.Project)
            .Where(x => x.Year == year && x.Month == month && userIds.Contains(x.UserId))
            .ToListAsync(ct);

        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        var minutes = await _db.DailyWorkEntries.AsNoTracking()
            .Where(x => x.WorkDate >= start && x.WorkDate < end)
            .GroupBy(x => x.UserId)
            .Select(g => new { g.Key, Minutes = g.Sum(x => x.DurationMinutes) })
            .ToDictionaryAsync(x => x.Key, x => x.Minutes, ct);

        var taskIds = sheets.SelectMany(x => x.Tasks).Select(x => x.Id).ToList();
        var actuals = await ActualMinutesAsync(taskIds, ct);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var term = search?.Trim().ToLower();

        var employees = users.Select(user =>
        {
            var sheet = sheets.FirstOrDefault(x => x.UserId == user.Id);
            var tasks = (sheet?.Tasks ?? []).Select(task => MapTask(task, actuals.GetValueOrDefault(task.Id), today)).ToList();
            if (projectId is Guid pid) tasks = tasks.Where(x => x.ProjectId == pid).ToList();
            if (!string.IsNullOrWhiteSpace(status)) tasks = tasks.Where(x => x.Status == status).ToList();
            if (!string.IsNullOrWhiteSpace(priority)) tasks = tasks.Where(x => x.Priority == priority).ToList();
            if (!string.IsNullOrWhiteSpace(term))
            {
                tasks = tasks.Where(x =>
                    x.Title.ToLower().Contains(term) ||
                    (x.Description?.ToLower().Contains(term) ?? false) ||
                    (x.ProjectName?.ToLower().Contains(term) ?? false) ||
                    user.Name.ToLower().Contains(term)).ToList();
            }

            var counts = UserService.CountTasks(tasks);
            var actualHours = WorkMetrics.Hours(minutes.GetValueOrDefault(user.Id));
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
                ActualHours = actualHours,
                CompletionPercent = counts.Completion,
                Tasks = tasks.OrderBy(x => x.PlannedStartDate).ThenBy(x => x.Title).ToList(),
                CompletedWork = tasks.Where(x => x.Status == TaskStatuses.Completed).ToList(),
                BlockedWork = tasks.Where(x => x.Status == TaskStatuses.Blocked).ToList(),
                Remarks = tasks.Select(x => x.Remarks).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToList()
            };
        }).ToList();

        return new TeamPlansDto { Year = year, Month = month, Employees = employees };
    }

    public async Task<MonthlyTaskDto> AddTaskAsync(Guid sheetId, SaveTaskRequest request, CancellationToken ct)
    {
        var sheet = await LoadEditableSheet(sheetId, ct);
        await EnsureProjectAsync(request.ProjectId, sheet.UserId, ct);
        EnsureDatesInMonth(sheet, request);

        var now = DateTime.UtcNow;
        var task = new MonthlyTask
        {
            Id = Guid.NewGuid(),
            MonthlyWorkSheetId = sheet.Id,
            ProjectId = request.ProjectId,
            Title = request.Title.Trim(),
            Description = WorkMetrics.Clean(request.Description),
            Priority = request.Priority,
            PlannedStartDate = request.PlannedStartDate,
            PlannedEndDate = request.PlannedEndDate,
            EstimatedHours = request.EstimatedHours,
            Status = request.Status,
            Remarks = WorkMetrics.Clean(request.Remarks),
            CreatedBy = _current.UserId,
            CreatedAt = now,
            UpdatedAt = now
        };
        sheet.UpdatedAt = now;
        _db.MonthlyTasks.Add(task);
        _audit.Stage(AuditActions.Created, "MonthlyTask", task.Id, $"Created task '{task.Title}'");
        await _db.SaveChangesAsync(ct);
        return await MapSingleTaskAsync(task.Id, ct);
    }

    public async Task<MonthlyTaskDto> UpdateTaskAsync(Guid taskId, SaveTaskRequest request, CancellationToken ct)
    {
        var task = await _db.MonthlyTasks.Include(x => x.Sheet).FirstOrDefaultAsync(x => x.Id == taskId, ct)
            ?? throw new AppException("Monthly task was not found.", StatusCodes.Status404NotFound);
        EnsureUnlocked(task.Sheet);
        await EnsureProjectAsync(request.ProjectId, task.Sheet.UserId, ct);
        EnsureDatesInMonth(task.Sheet, request);

        task.ProjectId = request.ProjectId;
        task.Title = request.Title.Trim();
        task.Description = WorkMetrics.Clean(request.Description);
        task.Priority = request.Priority;
        task.PlannedStartDate = request.PlannedStartDate;
        task.PlannedEndDate = request.PlannedEndDate;
        task.EstimatedHours = request.EstimatedHours;
        task.Status = request.Status;
        task.Remarks = WorkMetrics.Clean(request.Remarks);
        task.UpdatedBy = _current.UserId;
        task.UpdatedAt = DateTime.UtcNow;
        task.Sheet.UpdatedAt = task.UpdatedAt;
        _audit.Stage(AuditActions.Updated, "MonthlyTask", task.Id, $"Updated task '{task.Title}' to {task.Status}");
        await _db.SaveChangesAsync(ct);
        return await MapSingleTaskAsync(task.Id, ct);
    }

    public async Task DeleteTaskAsync(Guid taskId, CancellationToken ct)
    {
        var task = await _db.MonthlyTasks.Include(x => x.Sheet).FirstOrDefaultAsync(x => x.Id == taskId, ct)
            ?? throw new AppException("Monthly task was not found.", StatusCodes.Status404NotFound);
        EnsureUnlocked(task.Sheet);
        if (await _db.DailyWorkEntries.AnyAsync(x => x.MonthlyTaskId == taskId, ct))
            throw new AppException("This task has daily work entries. Cancel it instead of deleting it.");

        _db.MonthlyTasks.Remove(task);
        _audit.Stage(AuditActions.Deleted, "MonthlyTask", task.Id, $"Deleted task '{task.Title}'");
        await _db.SaveChangesAsync(ct);
    }

    public async Task<MonthlySheetDto> SetLockAsync(Guid sheetId, bool locked, CancellationToken ct)
    {
        _current.EnsureLead();
        var sheet = await _db.MonthlyWorkSheets.FirstOrDefaultAsync(x => x.Id == sheetId, ct)
            ?? throw new AppException("Monthly work sheet was not found.", StatusCodes.Status404NotFound);
        await _access.EnsureCanManageUserAsync(sheet.UserId, ct);

        sheet.Status = locked ? SheetStatuses.Locked : SheetStatuses.Open;
        sheet.LockedAt = locked ? DateTime.UtcNow : null;
        sheet.LockedBy = locked ? _current.UserId : null;
        sheet.UpdatedAt = DateTime.UtcNow;
        _audit.Stage(locked ? AuditActions.Locked : AuditActions.Unlocked, "MonthlyWorkSheet", sheet.Id, $"{sheet.Year}-{sheet.Month:00}");
        await _db.SaveChangesAsync(ct);
        return await GetAsync(sheet.Id, ct);
    }

    private async Task<MonthlyWorkSheet> LoadEditableSheet(Guid sheetId, CancellationToken ct)
    {
        var sheet = await _db.MonthlyWorkSheets.FirstOrDefaultAsync(x => x.Id == sheetId, ct)
            ?? throw new AppException("Monthly work sheet was not found.", StatusCodes.Status404NotFound);
        await _access.EnsureSameDepartmentAsync(sheet.UserId, ct);
        EnsureUnlocked(sheet);
        return sheet;
    }

    private static void EnsureUnlocked(MonthlyWorkSheet sheet)
    {
        if (sheet.Status == SheetStatuses.Locked)
            throw new AppException("This month is locked. The Team Lead must unlock it before changes can be made.");
    }

    private static void EnsureDatesInMonth(MonthlyWorkSheet sheet, SaveTaskRequest request)
    {
        var first = new DateOnly(sheet.Year, sheet.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        if (request.PlannedStartDate < first || request.PlannedEndDate > last)
            throw new AppException("Planned dates must fall within this work sheet's month.");
    }

    private async Task EnsureProjectAsync(Guid? projectId, Guid ownerId, CancellationToken ct)
    {
        if (projectId is null) return;
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId, ct)
            ?? throw new AppException("Project was not found.", StatusCodes.Status404NotFound);
        var ownerDepartment = await _db.Users.AsNoTracking().Where(x => x.Id == ownerId).Select(x => x.DepartmentId).FirstOrDefaultAsync(ct);
        if (project.DepartmentId != ownerDepartment)
            throw new AppException("That project belongs to another department.");
    }

    private async Task<MonthlySheetDto> MapSheetAsync(MonthlyWorkSheet sheet, CancellationToken ct)
    {
        var tasks = sheet.Tasks.ToList();
        var actuals = await ActualMinutesAsync(tasks.Select(x => x.Id).ToList(), ct);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var mapped = tasks.Select(task => MapTask(task, actuals.GetValueOrDefault(task.Id), today))
            .OrderBy(x => x.PlannedStartDate).ThenBy(x => x.Title).ToList();
        var counts = UserService.CountTasks(mapped);
        var actualMinutes = actuals.Values.Sum();
        return new MonthlySheetDto
        {
            Id = sheet.Id,
            UserId = sheet.UserId,
            UserName = sheet.User.Name,
            JobTitle = sheet.User.JobTitle,
            Year = sheet.Year,
            Month = sheet.Month,
            Status = sheet.Status,
            LockedAt = sheet.LockedAt,
            PlannedTasks = counts.Total,
            Completed = counts.Completed,
            InProgress = counts.InProgress,
            Blocked = counts.Blocked,
            Cancelled = counts.Cancelled,
            Planned = counts.Planned,
            PlannedHours = counts.PlannedHours,
            ActualHours = WorkMetrics.Hours(actualMinutes),
            CompletionPercent = counts.Completion,
            Tasks = mapped
        };
    }

    private async Task<MonthlyTaskDto> MapSingleTaskAsync(Guid taskId, CancellationToken ct)
    {
        var task = await _db.MonthlyTasks.AsNoTracking().Include(x => x.Project).FirstAsync(x => x.Id == taskId, ct);
        var minutes = await _db.DailyWorkEntries.Where(x => x.MonthlyTaskId == taskId).SumAsync(x => (int?)x.DurationMinutes, ct) ?? 0;
        return MapTask(task, minutes, DateOnly.FromDateTime(DateTime.Today));
    }

    private async Task<Dictionary<Guid, int>> ActualMinutesAsync(List<Guid> taskIds, CancellationToken ct)
    {
        if (taskIds.Count == 0) return [];
        return await _db.DailyWorkEntries.AsNoTracking()
            .Where(x => taskIds.Contains(x.MonthlyTaskId))
            .GroupBy(x => x.MonthlyTaskId)
            .Select(g => new { g.Key, Minutes = g.Sum(x => x.DurationMinutes) })
            .ToDictionaryAsync(x => x.Key, x => x.Minutes, ct);
    }

    public static MonthlyTaskDto MapTask(MonthlyTask task, int minutes, DateOnly today) => new()
    {
        Id = task.Id,
        MonthlyWorkSheetId = task.MonthlyWorkSheetId,
        ProjectId = task.ProjectId,
        ProjectName = task.Project?.Name,
        ProjectCode = task.Project?.Code,
        Title = task.Title,
        Description = task.Description,
        Priority = task.Priority,
        PlannedStartDate = task.PlannedStartDate,
        PlannedEndDate = task.PlannedEndDate,
        EstimatedHours = task.EstimatedHours,
        ActualHours = WorkMetrics.Hours(minutes),
        Status = task.Status,
        Remarks = task.Remarks,
        Overdue = task.PlannedEndDate < today && TaskStatuses.Open.Contains(task.Status),
        CreatedAt = task.CreatedAt,
        UpdatedAt = task.UpdatedAt
    };
}
