using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IOrganizationService
{
    Task<OrganizationDashboardDto> GetAsync(int year, int month, Guid? departmentId, Guid? projectId, string? status, string? projectStatus, DateOnly? dueBefore, Guid? userId, CancellationToken ct);
}

public class OrganizationService : IOrganizationService
{
    private readonly AppDbContext _db;
    private readonly IDepartmentAccess _access;
    private readonly IConfiguration _config;

    public OrganizationService(AppDbContext db, IDepartmentAccess access, IConfiguration config)
    {
        _db = db;
        _access = access;
        _config = config;
    }

    public async Task<OrganizationDashboardDto> GetAsync(int year, int month, Guid? departmentId, Guid? projectId, string? status, string? projectStatus, DateOnly? dueBefore, Guid? userId, CancellationToken ct)
    {
        if (!await _access.CanViewOrganizationOverviewAsync(ct))
            throw new AppException("You do not have access to the organization overview.", StatusCodes.Status403Forbidden);
        if (month is < 1 or > 12) throw new AppException("Month must be between 1 and 12.");

        var staleDays = _config.GetValue("Executive:StaleProgressDays", ExecutiveMetrics.DefaultStaleProgressDays);
        if (staleDays < 1) staleDays = ExecutiveMetrics.DefaultStaleProgressDays;

        var departments = await _db.Departments.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        var leads = await _db.Users.AsNoTracking()
            .Where(x => x.IsActive && x.Role == Roles.TeamLead && x.DepartmentId != null)
            .Select(x => new { x.DepartmentId, x.Name })
            .ToListAsync(ct);
        var departmentsWithPeople = await _db.Users.AsNoTracking()
            .Where(x => x.IsActive && x.DepartmentId != null)
            .Select(x => x.DepartmentId!.Value)
            .Distinct()
            .ToListAsync(ct);
        var projects = await _db.Projects.AsNoTracking().Include(x => x.Department).Include(x => x.Owner).ToListAsync(ct);
        var planIds = await _db.DepartmentMonthlyPlans.AsNoTracking()
            .Where(x => x.Year == year && x.Month == month)
            .Select(x => x.DepartmentId)
            .ToListAsync(ct);
        var tasks = await _db.MonthlyTasks.AsNoTracking()
            .Include(x => x.Project)
            .Include(x => x.Sheet).ThenInclude(x => x.Department)
            .Include(x => x.Sheet).ThenInclude(x => x.User).ThenInclude(x => x.Department)
            .Where(x => x.Sheet.Year == year && x.Sheet.Month == month)
            .ToListAsync(ct);

        bool Match(MonthlyTask task)
        {
            var person = task.Sheet.User;
            if (departmentId is Guid dept && WorkRecords.ReportingDepartment(task.Sheet.DepartmentId, person.DepartmentId) != dept) return false;
            if (projectId is Guid project && task.ProjectId != project) return false;
            if (userId is Guid personId && person.Id != personId) return false;
            if (!string.IsNullOrWhiteSpace(status) && task.Status != status) return false;
            return true;
        }

        var visible = tasks.Where(Match).ToList();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var soon = today.AddDays(7);
        var counted = visible.Where(x => ExecutiveMetrics.CountsTowardPlan(x.Status)).ToList();
        var completed = counted.Count(x => x.Status == TaskStatuses.Completed);
        var completion = WorkRecords.PlanCompletion(counted.Select(x => (x.Status, x.ProgressPercent, x.EstimatedHours)));
        var departmentRows = departments
            .Where(x => departmentId is null || departmentId == Guid.Empty || x.Id == departmentId)
            .Select(department =>
            {
                var goals = visible.Where(x => WorkRecords.ReportingDepartment(x.Sheet.DepartmentId, x.Sheet.User.DepartmentId) == department.Id).ToList();
                var activeGoals = goals.Where(x => ExecutiveMetrics.CountsTowardPlan(x.Status)).ToList();
                var done = activeGoals.Count(x => x.Status == TaskStatuses.Completed);
                var rate = WorkRecords.PlanCompletion(activeGoals.Select(x => (x.Status, x.ProgressPercent, x.EstimatedHours)));
                return new OrganizationDepartmentDto
                {
                    DepartmentId = department.Id,
                    Name = department.Name,
                    IsActive = department.IsActive,
                    TeamLeadName = leads.FirstOrDefault(x => x.DepartmentId == department.Id)?.Name,
                    ActiveProjects = projects.Count(x => x.DepartmentId == department.Id && x.Status == ProjectStatuses.Active),
                    PlannedGoals = activeGoals.Count,
                    CompletedGoals = done,
                    InProgressGoals = activeGoals.Count(x => x.Status == TaskStatuses.InProgress),
                    CancelledGoals = goals.Count(x => x.Status == TaskStatuses.Cancelled),
                    BlockedGoals = goals.Count(x => x.Status == TaskStatuses.Blocked),
                    OverdueGoals = goals.Count(x => ExecutiveMetrics.IsOverdue(x.Status, x.PlannedEndDate, today)),
                    HasGoals = activeGoals.Count > 0,
                    HasMonthlyPlan = planIds.Contains(department.Id),
                    CompletionPercent = rate ?? 0
                };
            })
            .ToList();

        var projectRows = projects
            .Where(x => departmentId is null || departmentId == Guid.Empty || x.DepartmentId == departmentId)
            .Where(x => projectId is null || projectId == Guid.Empty || x.Id == projectId)
            .Where(x => string.IsNullOrWhiteSpace(projectStatus) || x.Status == projectStatus)
            .Where(x => dueBefore is null || (x.EndDate != null && x.EndDate <= dueBefore))
            .Select(project =>
            {
                var goals = visible.Where(x => x.ProjectId == project.Id).ToList();
                var activeGoals = goals.Where(x => ExecutiveMetrics.CountsTowardPlan(x.Status)).ToList();
                var overdue = goals.Count(x => ExecutiveMetrics.IsOverdue(x.Status, x.PlannedEndDate, today));
                var blocked = goals.Count(x => x.Status == TaskStatuses.Blocked);
                var pastTarget = project.EndDate is DateOnly end && end < today && project.Status is ProjectStatuses.Active or ProjectStatuses.OnHold;
                var reasons = new List<string>();
                if (pastTarget) reasons.Add("Past target date");
                if (blocked > 0) reasons.Add($"{blocked} blocked goal{(blocked == 1 ? "" : "s")}");
                if (overdue > 0) reasons.Add($"{overdue} overdue goal{(overdue == 1 ? "" : "s")}");
                var progressRate = WorkRecords.PlanCompletion(activeGoals.Select(x => (x.Status, x.ProgressPercent, x.EstimatedHours)));
                var progress = progressRate is null ? 0 : (int)Math.Round(progressRate.Value, MidpointRounding.AwayFromZero);
                return new OrganizationProjectDto
                {
                    ProjectId = project.Id,
                    Name = project.Name,
                    DepartmentId = project.DepartmentId,
                    DepartmentName = project.Department?.Name,
                    OwnerName = project.Owner?.Name,
                    Status = project.Status,
                    Priority = project.Priority,
                    StartDate = project.StartDate,
                    EndDate = project.EndDate,
                    ProgressPercent = progress,
                    HasGoals = activeGoals.Count > 0,
                    AtRisk = ExecutiveMetrics.ProjectAtRisk(project.Status, project.EndDate, today, blocked > 0, overdue > 0),
                    RiskSummary = reasons.Count == 0 ? null : string.Join(" · ", reasons),
                    PlannedGoals = activeGoals.Count,
                    CompletedGoals = activeGoals.Count(x => x.Status == TaskStatuses.Completed),
                    InProgressGoals = activeGoals.Count(x => x.Status == TaskStatuses.InProgress),
                    BlockedGoals = blocked,
                    OverdueGoals = overdue
                };
            })
            .OrderBy(x => x.DepartmentName).ThenBy(x => x.Name)
            .ToList();

        GoalAlertDto Alert(MonthlyTask task) => new()
        {
            TaskId = task.Id,
            Title = task.Title,
            UserId = task.Sheet.UserId,
            Employee = task.Sheet.User.Name,
            DepartmentId = WorkRecords.ReportingDepartment(task.Sheet.DepartmentId, task.Sheet.User.DepartmentId),
            Department = task.Sheet.Department?.Name ?? task.Sheet.User.Department?.Name ?? "No department",
            ProjectId = task.ProjectId,
            Project = task.Project?.Name,
            Due = task.PlannedEndDate,
            Status = task.Status,
            Remarks = task.Remarks
        };

        var trend = new List<OrganizationTrendDto>();
        for (var offset = 5; offset >= 0; offset--)
        {
            var cursor = new DateOnly(year, month, 1).AddMonths(-offset);
            var monthTasks = await _db.MonthlyTasks.AsNoTracking()
                .Where(x => x.Sheet.Year == cursor.Year && x.Sheet.Month == cursor.Month)
                .Where(x => departmentId == null || departmentId == Guid.Empty || x.Sheet.DepartmentId == departmentId || (x.Sheet.DepartmentId == null && x.Sheet.User.DepartmentId == departmentId))
                .Where(x => projectId == null || projectId == Guid.Empty || x.ProjectId == projectId)
                .Where(x => userId == null || userId == Guid.Empty || x.Sheet.UserId == userId)
                .Where(x => string.IsNullOrWhiteSpace(status) || x.Status == status)
                .Select(x => new { x.Status, x.ProgressPercent, x.EstimatedHours })
                .ToListAsync(ct);
            var countedMonth = monthTasks.Count(x => ExecutiveMetrics.CountsTowardPlan(x.Status));
            var done = monthTasks.Count(x => x.Status == TaskStatuses.Completed);
            trend.Add(new OrganizationTrendDto
            {
                Year = cursor.Year,
                Month = cursor.Month,
                PlannedGoals = countedMonth,
                CompletedGoals = done,
                CompletionPercent = WorkRecords.PlanCompletion(monthTasks.Select(x => (x.Status, x.ProgressPercent, x.EstimatedHours))) ?? 0
            });
        }

        var previous = trend.Count >= 2 ? trend[^2] : null;
        var previousRate = previous is { PlannedGoals: > 0 } ? previous.CompletionPercent : (decimal?)null;
        var risks = new List<ExecutiveRiskDto>();
        foreach (var goal in visible.Where(x => ExecutiveMetrics.IsOverdue(x.Status, x.PlannedEndDate, today)))
            risks.Add(Risk("OverdueGoal", "High", goal, "Overdue"));
        foreach (var goal in visible.Where(x => x.Status == TaskStatuses.Blocked))
            risks.Add(Risk("Blocked", "High", goal, goal.Remarks ?? "Blocked"));
        foreach (var goal in visible.Where(x => TaskStatuses.Open.Contains(x.Status) && ExecutiveMetrics.IsStale(x.UpdatedAt, DateTime.UtcNow, staleDays)))
            risks.Add(Risk("StaleProgress", "Medium", goal, $"No progress update in {staleDays} days"));
        foreach (var project in projectRows.Where(x => x.EndDate is DateOnly end && end < today && x.Status is ProjectStatuses.Active or ProjectStatuses.OnHold))
            risks.Add(new ExecutiveRiskDto
            {
                Kind = "PastTarget",
                Severity = "High",
                Title = project.Name,
                DepartmentId = project.DepartmentId,
                Department = project.DepartmentName,
                ProjectId = project.ProjectId,
                Project = project.Name,
                Due = project.EndDate,
                Status = project.Status,
                Summary = "Target date has passed"
            });
        foreach (var department in departmentRows.Where(x => x.IsActive && !x.HasMonthlyPlan && departmentsWithPeople.Contains(x.DepartmentId)))
            risks.Add(new ExecutiveRiskDto
            {
                Kind = "MissingPlan",
                Severity = "Medium",
                Title = department.Name,
                DepartmentId = department.DepartmentId,
                Department = department.Name,
                Summary = "No department monthly plan for this period"
            });

        return new OrganizationDashboardDto
        {
            Year = year,
            Month = month,
            DepartmentCount = departmentRows.Count(x => x.IsActive),
            ActiveProjectCount = projects.Count(x => x.Status == ProjectStatuses.Active && (departmentId is null || departmentId == Guid.Empty || x.DepartmentId == departmentId)),
            PlannedGoals = counted.Count,
            CompletedGoals = completed,
            InProgressGoals = counted.Count(x => x.Status == TaskStatuses.InProgress),
            CancelledGoals = visible.Count(x => x.Status == TaskStatuses.Cancelled),
            ProjectsAtRisk = projectRows.Count(x => x.AtRisk),
            HasGoals = counted.Count > 0,
            CompletionPercent = completion ?? 0,
            PreviousCompletionPercent = previousRate,
            CompletionChange = completion is decimal current && previousRate is decimal prior ? current - prior : null,
            StaleProgressDays = staleDays,
            GeneratedAt = DateTime.UtcNow,
            ProgressRule = "Plan completion is the share of estimated hours marked done. Each goal contributes its done percentage times its estimated hours. A completed goal counts as 100%, and cancelled goals are left out. Logged time does not change it. A project is at risk only when it is active or on hold and it is past its target date, has a blocked goal, or has an overdue goal.",
            OverdueGoals = visible.Count(x => ExecutiveMetrics.IsOverdue(x.Status, x.PlannedEndDate, today)),
            BlockedGoals = visible.Count(x => x.Status == TaskStatuses.Blocked),
            Departments = departmentRows,
            Projects = projectRows,
            Overdue = visible.Where(x => x.PlannedEndDate < today && TaskStatuses.Open.Contains(x.Status)).OrderBy(x => x.PlannedEndDate).Take(12).Select(Alert).ToList(),
            Upcoming = visible.Where(x => TaskStatuses.Open.Contains(x.Status) && x.PlannedEndDate >= today && x.PlannedEndDate <= soon).OrderBy(x => x.PlannedEndDate).Take(12).Select(Alert).ToList(),
            Blockers = visible.Where(x => x.Status == TaskStatuses.Blocked).Take(12).Select(Alert).ToList(),
            Workload = visible.GroupBy(x => x.Sheet.User).Select(group => new OrganizationWorkloadDto
            {
                UserId = group.Key.Id,
                Name = group.Key.Name,
                Department = group.Key.Department?.Name,
                Goals = group.Count(),
                Completed = group.Count(x => x.Status == TaskStatuses.Completed),
                Blocked = group.Count(x => x.Status == TaskStatuses.Blocked)
            }).OrderBy(x => x.Name).ToList(),
            Trend = trend,
            Plans = await _db.DepartmentMonthlyPlans.AsNoTracking().Include(x => x.Department)
                .Where(x => x.Year == year && x.Month == month)
                .Where(x => departmentId == null || departmentId == Guid.Empty || x.DepartmentId == departmentId)
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
                .ToListAsync(ct),
            Risks = risks.Take(200).ToList()
        };
    }

    private static ExecutiveRiskDto Risk(string kind, string severity, MonthlyTask goal, string summary) => new()
    {
        Kind = kind,
        Severity = severity,
        Title = goal.Title,
        DepartmentId = WorkRecords.ReportingDepartment(goal.Sheet.DepartmentId, goal.Sheet.User.DepartmentId),
        Department = goal.Sheet.Department?.Name ?? goal.Sheet.User.Department?.Name ?? "No department",
        ProjectId = goal.ProjectId,
        Project = goal.Project?.Name,
        UserId = goal.Sheet.UserId,
        Due = goal.PlannedEndDate,
        Status = goal.Status,
        Summary = summary
    };
}
