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
    Task<TeamPlansDto> TeamAsync(int year, int month, Guid? userId, Guid? projectId, string? status, string? priority, string? search, Guid? departmentId, CancellationToken ct, bool organizationWide = false);
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
    private readonly IInitiativeService _initiatives;

    public MonthlyWorkService(AppDbContext db, ICurrentUser current, IAuditService audit, IDepartmentAccess access, IInitiativeService initiatives)
    {
        _db = db;
        _current = current;
        _audit = audit;
        _access = access;
        _initiatives = initiatives;
    }

    public async Task<MonthlySheetDto> EnsureAsync(EnsureSheetRequest request, CancellationToken ct)
    {
        var target = request.UserId is null || request.UserId == Guid.Empty ? _current.UserId : request.UserId.Value;
        await _access.EnsureCanViewPlanAsync(target, ct);
        if (!await _db.Users.AnyAsync(x => x.Id == target && x.IsActive, ct))
            throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);

        var existing = await _db.MonthlyWorkSheets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == target && x.Year == request.Year && x.Month == request.Month, ct);
        if (existing is not null)
        {
            var tracked = await _db.MonthlyWorkSheets.FirstAsync(x => x.Id == existing.Id, ct);
            if (tracked.DepartmentPlanId is null)
            {
                await LinkDepartmentPlanAsync(tracked, ct);
                await _db.SaveChangesAsync(ct);
            }
            return await GetAsync(existing.Id, ct);
        }

        if (target != _current.UserId)
        {
            var actorRole = await _db.Users.AsNoTracking().Where(x => x.Id == _current.UserId).Select(x => x.Role).FirstOrDefaultAsync(ct) ?? "";
            if (!DepartmentRules.CanEditDepartmentWork(actorRole))
                throw new AppException("That employee has no monthly plan for this month.");
        }

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
        await LinkDepartmentPlanAsync(sheet, ct);
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
            .Include(x => x.User).ThenInclude(x => x.Department)
            .Include(x => x.Department)
            .Include(x => x.Tasks).ThenInclude(x => x.Project)
            .Include(x => x.Tasks).ThenInclude(x => x.WorkCategory)
            .Include(x => x.Tasks).ThenInclude(x => x.Initiative)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Monthly work sheet was not found.", StatusCodes.Status404NotFound);
        await _access.EnsureCanViewPlanAsync(sheet.UserId, ct);

        return await HideOthersDailyAsync(await MapSheetAsync(sheet, ct), ct);
    }

    public async Task<TeamPlansDto> TeamAsync(int year, int month, Guid? userId, Guid? projectId, string? status, string? priority, string? search, Guid? departmentId, CancellationToken ct, bool organizationWide = false)
    {
        if (month is < 1 or > 12) throw new AppException("Month must be between 1 and 12.");
        var viewer = await _access.IsOrganizationViewerAsync(ct);
        if (organizationWide && !viewer && !await _access.CanViewOrganizationReportsAsync(ct))
            throw new AppException("You do not have access to organization reports.", StatusCodes.Status403Forbidden);
        var across = viewer || organizationWide;

        var usersQuery = _db.Users.AsNoTracking().Include(x => x.Department).Where(x => x.IsActive);
        if (across)
        {
            if (departmentId is Guid id && id != Guid.Empty)
                usersQuery = usersQuery.Where(x => x.DepartmentId == id);
        }
        else
            usersQuery = await _access.ScopeUsersAsync(usersQuery, departmentId, ct);
        if (userId is Guid filterUser) usersQuery = usersQuery.Where(x => x.Id == filterUser);
        var users = await usersQuery.OrderBy(x => x.Name).ToListAsync(ct);
        var userIds = users.Select(x => x.Id).ToList();
        var scopeDepartment = departmentId is Guid chosen && chosen != Guid.Empty ? chosen : (Guid?)null;
        if (scopeDepartment is null && !across)
            scopeDepartment = await _access.MyDepartmentAsync(ct);
        if (scopeDepartment is Guid historicalDepartment)
        {
            var historicalIds = await _db.MonthlyWorkSheets.AsNoTracking()
                .Where(x => x.Year == year && x.Month == month && x.DepartmentId == historicalDepartment && !userIds.Contains(x.UserId))
                .Select(x => x.UserId)
                .ToListAsync(ct);
            if (historicalIds.Count > 0)
            {
                users.AddRange(await _db.Users.AsNoTracking().Include(x => x.Department).Where(x => historicalIds.Contains(x.Id)).ToListAsync(ct));
                userIds.AddRange(historicalIds);
            }
        }

        var sheets = await _db.MonthlyWorkSheets.AsNoTracking()
            .Include(x => x.Department)
            .Include(x => x.Tasks).ThenInclude(x => x.Project)
            .Include(x => x.Tasks).ThenInclude(x => x.WorkCategory)
            .Include(x => x.Tasks).ThenInclude(x => x.Initiative)
            .Where(x => x.Year == year && x.Month == month && userIds.Contains(x.UserId))
            .ToListAsync(ct);

        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        var showDaily = viewer;
        var minuteUserIds = showDaily ? userIds : userIds.Where(x => x == _current.UserId).ToList();
        var minutes = await _db.DailyWorkEntries.AsNoTracking()
            .Where(x => minuteUserIds.Contains(x.UserId) && x.WorkDate >= start && x.WorkDate < end)
            .GroupBy(x => x.UserId)
            .Select(g => new { g.Key, Minutes = g.Sum(x => x.DurationMinutes) })
            .ToDictionaryAsync(x => x.Key, x => x.Minutes, ct);

        var taskIds = sheets
            .Where(x => showDaily || x.UserId == _current.UserId)
            .SelectMany(x => x.Tasks).Select(x => x.Id).ToList();
        var actuals = await ActualMinutesAsync(taskIds, ct);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var term = search?.Trim().ToLower();

        var employees = users.Select(user =>
        {
            var sheet = sheets.FirstOrDefault(x => x.UserId == user.Id);
            var reportingDepartment = WorkRecords.ReportingDepartment(sheet?.DepartmentId, user.DepartmentId);
            if (scopeDepartment is Guid required && reportingDepartment != required) return null;
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
            var shareDaily = showDaily || user.Id == _current.UserId;
            if (!shareDaily)
            {
                foreach (var task in tasks) task.ActualHours = 0;
            }
            var actualHours = shareDaily ? WorkMetrics.Hours(minutes.GetValueOrDefault(user.Id)) : 0;
            var person = UserMapper.ToDto(user);
            if (sheet?.DepartmentId is Guid stamped)
            {
                person.DepartmentId = stamped;
                person.DepartmentName = sheet.Department?.Name ?? person.DepartmentName;
            }
            return new EmployeeOverviewDto
            {
                User = person,
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
        }).Where(x => x is not null).Select(x => x!).ToList();

        var departmentIds = users.Where(x => x.DepartmentId != null).Select(x => x.DepartmentId!.Value)
            .Concat(sheets.Where(x => x.DepartmentId != null).Select(x => x.DepartmentId!.Value))
            .Distinct()
            .ToArray();
        if (scopeDepartment is Guid only) departmentIds = [only];
        var departmentPlans = await _db.DepartmentMonthlyPlans.AsNoTracking()
            .Include(x => x.Department)
            .Where(x => x.Year == year && x.Month == month && departmentIds.Contains(x.DepartmentId))
            .OrderBy(x => x.Department.Name)
            .Select(x => new DepartmentPlanDto
            {
                Id = x.Id,
                DepartmentId = x.DepartmentId,
                DepartmentName = x.Department.Name,
                Year = x.Year,
                Month = x.Month,
                Status = x.Status,
                Summary = x.Summary,
                Risks = x.Risks,
                Comments = x.Comments
            })
            .ToListAsync(ct);

        return new TeamPlansDto { Year = year, Month = month, DepartmentPlans = departmentPlans, Employees = employees };
    }

    public async Task<MonthlyTaskDto> AddTaskAsync(Guid sheetId, SaveTaskRequest request, CancellationToken ct)
    {
        var sheet = await LoadEditableSheet(sheetId, ct);
        var linked = await LinkAsync(request, sheet, null, ct);
        await EnsureProjectAsync(linked.ProjectId, sheet, ct);
        await EnsureUniqueGoalAsync(sheet.Id, request.Title, linked.ProjectId, null, ct);
        EnsureDatesInMonth(sheet, request);
        var applied = WorkRecords.ApplyGoalProgress(TaskStatuses.Planned, request.Status, request.ProgressPercent, null, null, DateOnly.FromDateTime(DateTime.Today));

        var now = DateTime.UtcNow;
        var task = new MonthlyTask
        {
            Id = Guid.NewGuid(),
            MonthlyWorkSheetId = sheet.Id,
            ProjectId = linked.ProjectId,
            WorkCategoryId = linked.CategoryId,
            InitiativeId = linked.InitiativeId,
            Title = request.Title.Trim(),
            Description = WorkMetrics.Clean(request.Description),
            Priority = request.Priority,
            PlannedStartDate = request.PlannedStartDate,
            PlannedEndDate = request.PlannedEndDate,
            ActualStartDate = applied.ActualStart,
            ActualEndDate = applied.ActualEnd,
            EstimatedHours = request.EstimatedHours,
            ProgressPercent = applied.ProgressPercent,
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
        await _access.EnsureCanEditPlanAsync(task.Sheet.UserId, ct);
        EnsureUnlocked(task.Sheet);
        var linked = await LinkAsync(request, task.Sheet, task.WorkCategoryId, ct);
        await EnsureProjectAsync(linked.ProjectId, task.Sheet, ct, task.ProjectId);
        await EnsureUniqueGoalAsync(task.Sheet.Id, request.Title, linked.ProjectId, task.Id, ct);
        EnsureDatesInMonth(task.Sheet, request);
        var previousStatus = task.Status;
        var previousProgress = task.ProgressPercent;
        var applied = WorkRecords.ApplyGoalProgress(previousStatus, request.Status, request.ProgressPercent, task.ActualStartDate, task.ActualEndDate, DateOnly.FromDateTime(DateTime.Today));

        task.ProjectId = linked.ProjectId;
        task.WorkCategoryId = linked.CategoryId;
        task.InitiativeId = linked.InitiativeId;
        task.Title = request.Title.Trim();
        task.Description = WorkMetrics.Clean(request.Description);
        task.Priority = request.Priority;
        task.PlannedStartDate = request.PlannedStartDate;
        task.PlannedEndDate = request.PlannedEndDate;
        task.ActualStartDate = applied.ActualStart;
        task.ActualEndDate = applied.ActualEnd;
        task.EstimatedHours = request.EstimatedHours;
        task.ProgressPercent = applied.ProgressPercent;
        task.Status = request.Status;
        task.Remarks = WorkMetrics.Clean(request.Remarks);
        task.UpdatedBy = _current.UserId;
        task.UpdatedAt = DateTime.UtcNow;
        task.Sheet.UpdatedAt = task.UpdatedAt;
        _audit.Stage(AuditActions.Updated, "MonthlyTask", task.Id, $"Updated '{task.Title}' from {previousStatus} to {task.Status}, progress {previousProgress} to {task.ProgressPercent}");
        await _db.SaveChangesAsync(ct);
        return await MapSingleTaskAsync(task.Id, ct);
    }

    public async Task DeleteTaskAsync(Guid taskId, CancellationToken ct)
    {
        var task = await _db.MonthlyTasks.Include(x => x.Sheet).FirstOrDefaultAsync(x => x.Id == taskId, ct)
            ?? throw new AppException("Monthly task was not found.", StatusCodes.Status404NotFound);
        await _access.EnsureCanEditPlanAsync(task.Sheet.UserId, ct);
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
        await _access.EnsureCanEditPlanAsync(sheet.UserId, ct);
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

    private async Task<(Guid? ProjectId, Guid? CategoryId, Guid? InitiativeId)> LinkAsync(SaveTaskRequest request, MonthlyWorkSheet sheet, Guid? currentCategoryId, CancellationToken ct)
    {
        var departmentId = sheet.DepartmentId ?? await _db.Users.AsNoTracking().Where(x => x.Id == sheet.UserId).Select(x => x.DepartmentId).FirstOrDefaultAsync(ct);
        var initiativeId = request.InitiativeId is Guid id && id != Guid.Empty ? id : (Guid?)null;
        if (departmentId is null)
        {
            if (initiativeId is not null || request.WorkCategoryId is Guid category && category != Guid.Empty)
                throw new AppException("This sheet has no department, so it cannot use a category or initiative.");
            return (request.ProjectId, null, null);
        }
        var linked = await _initiatives.LinkGoalAsync(initiativeId, request.ProjectId, request.WorkCategoryId, currentCategoryId, departmentId.Value, ct);
        return (linked.ProjectId, linked.CategoryId, initiativeId);
    }

    private async Task EnsureProjectAsync(Guid? projectId, MonthlyWorkSheet sheet, CancellationToken ct, Guid? currentProjectId = null)
    {
        if (projectId is null || projectId == Guid.Empty) return;
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId, ct)
            ?? throw new AppException("Project was not found.", StatusCodes.Status404NotFound);
        if (project.Id != currentProjectId && !WorkRecords.CanAssignProject(project.Status))
            throw new AppException("New goals can only use an active or on-hold project.");
        var ownerDepartment = sheet.DepartmentId ?? await _db.Users.AsNoTracking().Where(x => x.Id == sheet.UserId).Select(x => x.DepartmentId).FirstOrDefaultAsync(ct);
        if (project.DepartmentId != ownerDepartment)
            throw new AppException("That project belongs to another department.");
    }

    private async Task EnsureUniqueGoalAsync(Guid sheetId, string title, Guid? projectId, Guid? ignoreTaskId, CancellationToken ct)
    {
        var existing = await _db.MonthlyTasks.AsNoTracking()
            .Where(x => x.MonthlyWorkSheetId == sheetId && x.Id != ignoreTaskId)
            .Select(x => new { x.Title, x.ProjectId, x.Status })
            .ToListAsync(ct);
        if (WorkRecords.IsDuplicateGoal(title, projectId, existing.Select(x => (x.Title, x.ProjectId, x.Status))))
            throw new AppException("This monthly sheet already has that goal on the same project.", StatusCodes.Status409Conflict);
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
            DepartmentName = sheet.Department?.Name ?? sheet.User.Department?.Name,
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
        var task = await _db.MonthlyTasks.AsNoTracking().Include(x => x.Project).Include(x => x.WorkCategory).Include(x => x.Initiative).FirstAsync(x => x.Id == taskId, ct);
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
        WorkCategoryId = task.WorkCategoryId,
        WorkCategoryName = task.WorkCategory?.Name,
        InitiativeId = task.InitiativeId,
        InitiativeNumber = task.Initiative?.Number,
        InitiativeTitle = task.Initiative?.Title,
        Title = task.Title,
        Description = task.Description,
        Priority = task.Priority,
        PlannedStartDate = task.PlannedStartDate,
        PlannedEndDate = task.PlannedEndDate,
        ActualStartDate = task.ActualStartDate,
        ActualEndDate = task.ActualEndDate,
        EstimatedHours = task.EstimatedHours,
        ActualHours = WorkMetrics.Hours(minutes),
        ProgressPercent = task.ProgressPercent,
        Status = task.Status,
        Remarks = task.Remarks,
        Overdue = task.PlannedEndDate < today && TaskStatuses.Open.Contains(task.Status),
        CreatedAt = task.CreatedAt,
        UpdatedAt = task.UpdatedAt
    };

    private async Task LinkDepartmentPlanAsync(MonthlyWorkSheet sheet, CancellationToken ct)
    {
        var departmentId = sheet.DepartmentId ?? await _db.Users.AsNoTracking()
            .Where(x => x.Id == sheet.UserId)
            .Select(x => x.DepartmentId)
            .FirstOrDefaultAsync(ct);
        if (departmentId is null) return;
        sheet.DepartmentId ??= departmentId;

        var plan = await _db.DepartmentMonthlyPlans
            .FirstOrDefaultAsync(x => x.DepartmentId == departmentId && x.Year == sheet.Year && x.Month == sheet.Month, ct);
        if (plan is null)
        {
            var now = DateTime.UtcNow;
            plan = new DepartmentMonthlyPlan
            {
                Id = Guid.NewGuid(),
                DepartmentId = departmentId.Value,
                Year = sheet.Year,
                Month = sheet.Month,
                Status = DepartmentPlanStatuses.Open,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.DepartmentMonthlyPlans.Add(plan);
        }

        sheet.DepartmentPlanId = plan.Id;
    }

    private async Task<MonthlySheetDto> HideOthersDailyAsync(MonthlySheetDto sheet, CancellationToken ct)
    {
        if (sheet.UserId == _current.UserId || await _access.IsOrganizationViewerAsync(ct)) return sheet;
        sheet.ActualHours = 0;
        foreach (var task in sheet.Tasks) task.ActualHours = 0;
        return sheet;
    }
}
