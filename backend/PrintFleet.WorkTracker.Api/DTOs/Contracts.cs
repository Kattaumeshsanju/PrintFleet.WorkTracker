using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.DTOs;

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

public class DepartmentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public Guid? TeamLeadId { get; set; }
    public string? TeamLeadName { get; set; }
    public int MemberCount { get; set; }
    public int ActiveProjectCount { get; set; }
}

public class SetDepartmentStatusRequest
{
    public bool IsActive { get; set; }
}

public class AssignDepartmentMemberRequest
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = Roles.TeamMember;
}

public class SaveDepartmentTeamRequest
{
    public List<DepartmentTeamMemberRequest> Members { get; set; } = [];
    public List<NewDepartmentMemberRequest> NewMembers { get; set; } = [];
}

public class DepartmentTeamMemberRequest
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = Roles.TeamMember;
}

public class NewDepartmentMemberRequest
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string? JobTitle { get; set; }
    public string Role { get; set; } = Roles.TeamMember;
}

public class SaveDepartmentRequest
{
    public string Name { get; set; } = "";
}

public class UserDto
{
    public Guid Id { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string? JobTitle { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool OrganizationOverview { get; set; }
    public bool OrganizationReports { get; set; }
}

public class SaveUserRequest
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Password { get; set; }
    public string? JobTitle { get; set; }
    public string Role { get; set; } = Roles.TeamMember;
    public Guid? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; } = "";
    public string Priority { get; set; } = Priorities.Medium;
    public Guid? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public Guid? WorkCategoryId { get; set; }
    public string? WorkCategoryName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SaveProjectRequest
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; } = ProjectStatuses.Active;
    public string Priority { get; set; } = Priorities.Medium;
    public Guid? OwnerId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? WorkCategoryId { get; set; }
}

public class EnsureSheetRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
    public Guid? UserId { get; set; }
}

public class MonthlyTaskDto
{
    public Guid Id { get; set; }
    public Guid MonthlyWorkSheetId { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string? ProjectCode { get; set; }
    public Guid? WorkCategoryId { get; set; }
    public string? WorkCategoryName { get; set; }
    public Guid? InitiativeId { get; set; }
    public string? InitiativeNumber { get; set; }
    public string? InitiativeTitle { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Priority { get; set; } = "";
    public DateOnly PlannedStartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    public decimal EstimatedHours { get; set; }
    public decimal ActualHours { get; set; }
    public int ProgressPercent { get; set; }
    public string Status { get; set; } = "";
    public string? Remarks { get; set; }
    public bool Overdue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MonthlySheetDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
    public string? JobTitle { get; set; }
    public string? DepartmentName { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Status { get; set; } = "";
    public DateTime? LockedAt { get; set; }
    public int PlannedTasks { get; set; }
    public int Completed { get; set; }
    public int InProgress { get; set; }
    public int Blocked { get; set; }
    public int Cancelled { get; set; }
    public int Planned { get; set; }
    public decimal PlannedHours { get; set; }
    public decimal ActualHours { get; set; }
    public decimal CompletionPercent { get; set; }
    public IReadOnlyList<MonthlyTaskDto> Tasks { get; set; } = [];
}

public class SaveTaskRequest
{
    public Guid? ProjectId { get; set; }
    public Guid? WorkCategoryId { get; set; }
    public Guid? InitiativeId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Priority { get; set; } = Priorities.Medium;
    public DateOnly PlannedStartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }
    public decimal EstimatedHours { get; set; }
    public int ProgressPercent { get; set; }
    public string Status { get; set; } = TaskStatuses.Planned;
    public string? Remarks { get; set; }
}

public class DailyEntryDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
    public Guid MonthlyTaskId { get; set; }
    public string MonthlyTaskTitle { get; set; } = "";
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public DateOnly WorkDate { get; set; }
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string Description { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Remarks { get; set; }
}

public class SaveDailyEntryRequest
{
    public DateOnly WorkDate { get; set; }
    public Guid MonthlyTaskId { get; set; }
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = EntryStatuses.InProgress;
    public string? Remarks { get; set; }
    public bool AllowOverlap { get; set; }
}

public class DailyWorkQuery
{
    public DateOnly? WorkDate { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class DayViewDto
{
    public DateOnly Date { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
    public string? JobTitle { get; set; }
    public int TotalMinutes { get; set; }
    public string SubmissionStatus { get; set; } = SubmissionStatuses.Pending;
    public DateTime? SubmittedAt { get; set; }
    public bool MonthLocked { get; set; }
    public bool CanEdit { get; set; }
    public string? DayType { get; set; }
    public IReadOnlyList<DailyEntryDto> Entries { get; set; } = [];
}

public class MonthTimelineDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
    public string? JobTitle { get; set; }
    public int TotalMinutes { get; set; }
    public IReadOnlyList<DayViewDto> Days { get; set; } = [];
}

public class MarkDayRequest
{
    public DateOnly WorkDate { get; set; }
    public string? DayType { get; set; }
}

public class SubmitDayRequest
{
    public DateOnly WorkDate { get; set; }
}

public class ReopenDayRequest
{
    public DateOnly WorkDate { get; set; }
    public Guid UserId { get; set; }
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class NotificationDto
{
    public Guid? Id { get; set; }
    public string Severity { get; set; } = "info";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Href { get; set; }
    public bool Unread { get; set; }
    public string? SenderName { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class MonthProgressDto
{
    public int PlannedTasks { get; set; }
    public int Completed { get; set; }
    public int InProgress { get; set; }
    public int Blocked { get; set; }
    public int Cancelled { get; set; }
    public int Planned { get; set; }
    public decimal PlannedHours { get; set; }
    public decimal ActualHours { get; set; }
    public decimal CompletionPercent { get; set; }
}

public class MyDashboardDto
{
    public DateOnly Date { get; set; }
    public int TodayMinutes { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksInProgress { get; set; }
    public string DailyUpdateStatus { get; set; } = SubmissionStatuses.Pending;
    public bool MonthLocked { get; set; }
    public DayViewDto Today { get; set; } = null!;
    public MonthProgressDto Month { get; set; } = null!;
    public IReadOnlyList<MonthlyTaskDto> UpcomingTasks { get; set; } = [];
    public IReadOnlyList<NotificationDto> Notifications { get; set; } = [];
}

public class TeamMemberDayDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string? JobTitle { get; set; }
    public string Role { get; set; } = "";
    public string UpdateStatus { get; set; } = SubmissionStatuses.Pending;
    public int TotalMinutes { get; set; }
    public string? CurrentActivity { get; set; }
    public string? CurrentTask { get; set; }
    public string? CurrentStart { get; set; }
    public string? CurrentEnd { get; set; }
    public string? CurrentStatus { get; set; }
    public IReadOnlyList<DailyEntryDto> Entries { get; set; } = [];
}

public class TeamDashboardDto
{
    public DateOnly Date { get; set; }
    public int MemberCount { get; set; }
    public int UpdatedToday { get; set; }
    public int TotalMinutesToday { get; set; }
    public int OpenTasks { get; set; }
    public int CompletedThisMonth { get; set; }
    public int BlockedTasks { get; set; }
    public IReadOnlyList<TeamMemberDayDto> Members { get; set; } = [];
    public IReadOnlyList<NotificationDto> Notifications { get; set; } = [];
}

public class CalendarDayDto
{
    public DateOnly Date { get; set; }
    public int Minutes { get; set; }
    public int EntryCount { get; set; }
    public bool Submitted { get; set; }
    public bool IsWeekend { get; set; }
    public bool Missing { get; set; }
    public bool LowHours { get; set; }
    public string? DayType { get; set; }
}

public class CalendarDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
    public IReadOnlyList<CalendarDayDto> Days { get; set; } = [];
}

public class StatusCountDto
{
    public string Status { get; set; } = "";
    public int Count { get; set; }
}

public class DailyHoursPointDto
{
    public DateOnly Date { get; set; }
    public int Minutes { get; set; }
    public bool Submitted { get; set; }
    public bool IsWeekend { get; set; }
}

public class MonthlyReportDto
{
    public UserDto Employee { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public string SheetStatus { get; set; } = SheetStatuses.Open;
    public int PlannedTasks { get; set; }
    public int Completed { get; set; }
    public int InProgress { get; set; }
    public int Blocked { get; set; }
    public int Cancelled { get; set; }
    public int Planned { get; set; }
    public decimal PlannedHours { get; set; }
    public decimal ActualHours { get; set; }
    public decimal AverageDailyHours { get; set; }
    public decimal CompletionPercent { get; set; }
    public int DaysLogged { get; set; }
    public IReadOnlyList<StatusCountDto> StatusDistribution { get; set; } = [];
    public IReadOnlyList<DailyHoursPointDto> DailyHours { get; set; } = [];
    public IReadOnlyList<MonthlyTaskDto> Tasks { get; set; } = [];
}

public class ProjectSummaryDto
{
    public string ProjectName { get; set; } = "";
    public int ActivityCount { get; set; }
    public int TotalMinutes { get; set; }
    public decimal TotalHours { get; set; }
}

public class RangeReportDto
{
    public UserDto Employee { get; set; } = null!;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int ActivityCount { get; set; }
    public int TotalMinutes { get; set; }
    public decimal TotalHours { get; set; }
    public IReadOnlyList<ProjectSummaryDto> Projects { get; set; } = [];
    public IReadOnlyList<DailyEntryDto> Activities { get; set; } = [];
}

public class TeamReportRowDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string? JobTitle { get; set; }
    public string Role { get; set; } = "";
    public int Planned { get; set; }
    public int Completed { get; set; }
    public int InProgress { get; set; }
    public int Blocked { get; set; }
    public int Cancelled { get; set; }
    public decimal PlannedHours { get; set; }
    public decimal ActualHours { get; set; }
    public decimal CompletionPercent { get; set; }
    public int AvailableDays { get; set; }
    public int DaysLogged { get; set; }
    public int DaysSubmitted { get; set; }
    public int MissingDays { get; set; }
    public int LowHourDays { get; set; }
    public int LeaveDays { get; set; }
    public int HolidayDays { get; set; }
    public decimal HoursAttainment { get; set; }
    public decimal UtilizationPercent { get; set; }
    public decimal UpdateCompliance { get; set; }
}

public class TeamReportDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public bool IncludesSelf { get; set; }
    public bool IncludesDailyMetrics { get; set; }
    public int PlannedTasks { get; set; }
    public int CompletedTasks { get; set; }
    public decimal PlannedHours { get; set; }
    public decimal ActualHours { get; set; }
    public decimal CompletionPercent { get; set; }
    public int AvailableDays { get; set; }
    public int DaysLogged { get; set; }
    public int MissingDays { get; set; }
    public int LowHourDays { get; set; }
    public int LeaveDays { get; set; }
    public int HolidayDays { get; set; }
    public int BlockedTasks { get; set; }
    public decimal HoursAttainment { get; set; }
    public decimal UtilizationPercent { get; set; }
    public decimal UpdateCompliance { get; set; }
    public decimal BlockedPercent { get; set; }
    public decimal ExpectedHoursPerDay { get; set; } = 8;
    public IReadOnlyList<TeamReportRowDto> Rows { get; set; } = [];
    public IReadOnlyList<StatusCountDto> StatusDistribution { get; set; } = [];
}

public class EmployeeOverviewDto
{
    public UserDto User { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public string SheetStatus { get; set; } = SheetStatuses.Open;
    public Guid? SheetId { get; set; }
    public int PlannedTasks { get; set; }
    public int Completed { get; set; }
    public int InProgress { get; set; }
    public int Blocked { get; set; }
    public int Cancelled { get; set; }
    public int Planned { get; set; }
    public decimal PlannedHours { get; set; }
    public decimal ActualHours { get; set; }
    public decimal CompletionPercent { get; set; }
    public IReadOnlyList<MonthlyTaskDto> Tasks { get; set; } = [];
    public IReadOnlyList<DailyEntryDto> RecentEntries { get; set; } = [];
    public IReadOnlyList<CalendarDayDto> HoursByDay { get; set; } = [];
    public IReadOnlyList<MonthlyTaskDto> CompletedWork { get; set; } = [];
    public IReadOnlyList<MonthlyTaskDto> BlockedWork { get; set; } = [];
    public IReadOnlyList<string> Remarks { get; set; } = [];
}

public class DepartmentPlanDto
{
    public Guid Id { get; set; }
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public int Year { get; set; }
    public int Month { get; set; }
    public string Status { get; set; } = "";
    public string? Summary { get; set; }
    public string? Risks { get; set; }
    public string? Comments { get; set; }
}

public class SetOrganizationRoleRequest
{
    public string Role { get; set; } = Roles.Ceo;
    public Guid? DepartmentId { get; set; }
}

public class SaveDepartmentPlanRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
    public Guid? DepartmentId { get; set; }
    public string Status { get; set; } = DepartmentPlanStatuses.Open;
    public string? Summary { get; set; }
    public string? Risks { get; set; }
    public string? Comments { get; set; }
}

public class TeamPlansDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public IReadOnlyList<DepartmentPlanDto> DepartmentPlans { get; set; } = [];
    public IReadOnlyList<EmployeeOverviewDto> Employees { get; set; } = [];
}

public class OrganizationDashboardDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int DepartmentCount { get; set; }
    public int ActiveProjectCount { get; set; }
    public int PlannedGoals { get; set; }
    public int CompletedGoals { get; set; }
    public decimal CompletionPercent { get; set; }
    public int OverdueGoals { get; set; }
    public int BlockedGoals { get; set; }
    public int InProgressGoals { get; set; }
    public int CancelledGoals { get; set; }
    public int ProjectsAtRisk { get; set; }
    public bool HasGoals { get; set; }
    public decimal? PreviousCompletionPercent { get; set; }
    public decimal? CompletionChange { get; set; }
    public int StaleProgressDays { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string ProgressRule { get; set; } = "";
    public IReadOnlyList<OrganizationDepartmentDto> Departments { get; set; } = [];
    public IReadOnlyList<OrganizationProjectDto> Projects { get; set; } = [];
    public IReadOnlyList<GoalAlertDto> Overdue { get; set; } = [];
    public IReadOnlyList<GoalAlertDto> Upcoming { get; set; } = [];
    public IReadOnlyList<GoalAlertDto> Blockers { get; set; } = [];
    public IReadOnlyList<OrganizationWorkloadDto> Workload { get; set; } = [];
    public IReadOnlyList<OrganizationTrendDto> Trend { get; set; } = [];
    public IReadOnlyList<DepartmentPlanDto> Plans { get; set; } = [];
    public IReadOnlyList<ExecutiveRiskDto> Risks { get; set; } = [];
}

public class OrganizationDepartmentDto
{
    public Guid DepartmentId { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public string? TeamLeadName { get; set; }
    public int ActiveProjects { get; set; }
    public int PlannedGoals { get; set; }
    public int CompletedGoals { get; set; }
    public int InProgressGoals { get; set; }
    public int CancelledGoals { get; set; }
    public int BlockedGoals { get; set; }
    public int OverdueGoals { get; set; }
    public bool HasGoals { get; set; }
    public bool HasMonthlyPlan { get; set; }
    public decimal CompletionPercent { get; set; }
}

public class OrganizationProjectDto
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = "";
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? OwnerName { get; set; }
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int ProgressPercent { get; set; }
    public bool HasGoals { get; set; }
    public bool AtRisk { get; set; }
    public string? RiskSummary { get; set; }
    public int PlannedGoals { get; set; }
    public int CompletedGoals { get; set; }
    public int InProgressGoals { get; set; }
    public int BlockedGoals { get; set; }
    public int OverdueGoals { get; set; }
}

public class ExecutiveRiskDto
{
    public string Kind { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Title { get; set; } = "";
    public Guid? DepartmentId { get; set; }
    public string? Department { get; set; }
    public Guid? ProjectId { get; set; }
    public string? Project { get; set; }
    public Guid? UserId { get; set; }
    public DateOnly? Due { get; set; }
    public string? Status { get; set; }
    public string? Summary { get; set; }
}

public class GoalAlertDto
{
    public Guid TaskId { get; set; }
    public string Title { get; set; } = "";
    public Guid UserId { get; set; }
    public string Employee { get; set; } = "";
    public Guid? DepartmentId { get; set; }
    public string Department { get; set; } = "";
    public Guid? ProjectId { get; set; }
    public string? Project { get; set; }
    public DateOnly Due { get; set; }
    public string Status { get; set; } = "";
    public string? Remarks { get; set; }
}

public class OrganizationWorkloadDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string? Department { get; set; }
    public int Goals { get; set; }
    public int Completed { get; set; }
    public int Blocked { get; set; }
}

public class OrganizationTrendDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int PlannedGoals { get; set; }
    public int CompletedGoals { get; set; }
    public decimal CompletionPercent { get; set; }
}

public class MyTeamDto
{
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public string? TeamLeadName { get; set; }
    public int ActiveMembers { get; set; }
    public int PendingInvitations { get; set; }
    public int ActiveProjects { get; set; }
    public bool CanManage { get; set; }
    public IReadOnlyList<TeamPersonDto> Members { get; set; } = [];
    public IReadOnlyList<TeamInviteDto> Invitations { get; set; } = [];
    public IReadOnlyList<TeamProjectOptionDto> Projects { get; set; } = [];
}

public class TeamPersonDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? JobTitle { get; set; }
    public string Role { get; set; } = "";
    public bool IsActive { get; set; }
    public string? DepartmentName { get; set; }
    public Guid? DepartmentId { get; set; }
}

public class TeamInviteDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? JobTitle { get; set; }
    public string Status { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public class TeamProjectOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}

public class AddTeamMemberRequest
{
    public Guid UserId { get; set; }
}

public class InviteTeamMemberRequest
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? JobTitle { get; set; }
}

public class AcceptInvitationRequest
{
    public string Token { get; set; } = "";
    public string Password { get; set; } = "";
}

public class SendTeamNoticeRequest
{
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Category { get; set; } = NoticeCategories.General;
    public string Channel { get; set; } = NoticeChannels.Both;
    public bool Everyone { get; set; }
    public bool CopyToMe { get; set; }
    public bool LinkPlan { get; set; }
    public Guid? ProjectId { get; set; }
    public List<Guid> UserIds { get; set; } = [];
}

public class TeamNoticeHistoryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Category { get; set; } = "";
    public string SenderName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int RecipientCount { get; set; }
    public string Channel { get; set; } = "";
    public string Status { get; set; } = "";
    public IReadOnlyList<string> FailedRecipients { get; set; } = [];
    public bool CanRetry { get; set; }
}

public class AuditLogDto
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public Guid? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WorkCategoryDto
{
    public Guid Id { get; set; }
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SaveWorkCategoryRequest
{
    public Guid? DepartmentId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public class ReorderWorkCategoriesRequest
{
    public Guid? DepartmentId { get; set; }
    public List<Guid> Ids { get; set; } = [];
}

public class ReassignWorkCategoryRequest
{
    public Guid TargetCategoryId { get; set; }
}

public class SaveInitiativeRequest
{
    public Guid? DepartmentId { get; set; }
    public Guid? WorkCategoryId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Status { get; set; } = InitiativeStatuses.Planned;
    public string Priority { get; set; } = Priorities.Medium;
    public Guid? OwnerId { get; set; }
    public List<Guid> CollaboratorIds { get; set; } = [];
    public DateOnly? TargetStart { get; set; }
    public DateOnly? TargetEnd { get; set; }
    public string? Remarks { get; set; }
    public string? ResearchObjective { get; set; }
    public string? Hypothesis { get; set; }
    public string? ResearchStage { get; set; }
    public string? SuccessCriteria { get; set; }
    public string? ExperimentPlan { get; set; }
    public string? Conclusion { get; set; }
}

public class InitiativeDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public Guid? WorkCategoryId { get; set; }
    public string? WorkCategoryName { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public Guid? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public IReadOnlyList<TeamPersonDto> Collaborators { get; set; } = [];
    public DateOnly? TargetStart { get; set; }
    public DateOnly? TargetEnd { get; set; }
    public string? Remarks { get; set; }
    public string? ResearchObjective { get; set; }
    public string? Hypothesis { get; set; }
    public string? ResearchStage { get; set; }
    public string? SuccessCriteria { get; set; }
    public string? ExperimentPlan { get; set; }
    public string? Conclusion { get; set; }
    public bool CanEdit { get; set; }
    public bool CanRecord { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IReadOnlyList<MilestoneDto> Milestones { get; set; } = [];
    public IReadOnlyList<ResearchRecordDto> Records { get; set; } = [];
}

public class MilestoneDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public Guid? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public DateOnly? TargetDate { get; set; }
    public string Status { get; set; } = "";
    public DateOnly? CompletedOn { get; set; }
}

public class SaveMilestoneRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public Guid? OwnerId { get; set; }
    public DateOnly? TargetDate { get; set; }
    public string Status { get; set; } = InitiativeStatuses.Planned;
}

public class ResearchRecordDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public DateOnly RecordedOn { get; set; }
    public string? Objective { get; set; }
    public string? Method { get; set; }
    public string? Findings { get; set; }
    public string? Outcome { get; set; }
    public string? EvidenceLinks { get; set; }
    public string? NextSteps { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SaveResearchRecordRequest
{
    public string Title { get; set; } = "";
    public DateOnly RecordedOn { get; set; }
    public string? Objective { get; set; }
    public string? Method { get; set; }
    public string? Findings { get; set; }
    public string? Outcome { get; set; }
    public string? EvidenceLinks { get; set; }
    public string? NextSteps { get; set; }
}

public class ResearchPortfolioDto
{
    public int ActiveInitiatives { get; set; }
    public int OnHold { get; set; }
    public int CompletedInPeriod { get; set; }
    public int MilestonesDue { get; set; }
    public int MilestonesOverdue { get; set; }
    public string ProgressRule { get; set; } = "Category progress is completed initiatives divided by initiatives that are not cancelled. It is not an average of percentages.";
    public IReadOnlyList<CountDto> ByStatus { get; set; } = [];
    public IReadOnlyList<CountDto> ByStage { get; set; } = [];
    public IReadOnlyList<CountDto> ByPriority { get; set; } = [];
    public IReadOnlyList<CategoryProgressDto> Categories { get; set; } = [];
    public IReadOnlyList<InitiativeDto> Deadlines { get; set; } = [];
    public IReadOnlyList<ResearchRecordDto> Outcomes { get; set; } = [];
}

public class CountDto
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
}

public class CategoryProgressDto
{
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public int Counted { get; set; }
    public int Completed { get; set; }
    public decimal? CompletionPercent { get; set; }
}

public class CreateCeoRequest
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? JobTitle { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SetGrantedRequest
{
    public bool Granted { get; set; }
}

public class OrganizationAccessDto
{
    public IReadOnlyList<OrganizationPersonDto> People { get; set; } = [];
    public IReadOnlyList<CeoInvitationDto> Invitations { get; set; } = [];
}

public class OrganizationPersonDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? DepartmentName { get; set; }
    public string Role { get; set; } = "";
    public string? JobTitle { get; set; }
    public bool IsActive { get; set; }
    public bool Overview { get; set; }
    public bool Reports { get; set; }
    public bool GrantsEditable { get; set; }
    public bool StatusEditable { get; set; }
}

public class ReminderDepartmentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
    public int ActiveMembers { get; set; }
}

public class PlanReminderSettingsDto
{
    public bool OutlookReady { get; set; }
    public string OutlookStatus { get; set; } = "";
    public string MailTransport { get; set; } = "None";
    public string Sender { get; set; } = "";
    public bool Enabled { get; set; }
    public bool MissingPlanEnabled { get; set; }
    public bool DepartmentSummaryEnabled { get; set; }
    public int Hour { get; set; }
    public int Minute { get; set; }
    public IReadOnlyList<int> Weekdays { get; set; } = [];
    public string TimeZone { get; set; } = "Asia/Kolkata";
    public int RecipientCount { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastFailureAt { get; set; }
    public string? LastFailureDetail { get; set; }
    public IReadOnlyList<ReminderDepartmentDto> Departments { get; set; } = [];
}

public class SavePlanReminderSettingsRequest
{
    public bool Enabled { get; set; }
    public bool MissingPlanEnabled { get; set; }
    public bool DepartmentSummaryEnabled { get; set; }
    public int Hour { get; set; } = 17;
    public int Minute { get; set; }
    public List<int> Weekdays { get; set; } = [];
    public List<ReminderDepartmentDto> Departments { get; set; } = [];
}

public class PlanReminderPreviewDto
{
    public string Subject { get; set; } = "";
    public string Text { get; set; } = "";
    public string Html { get; set; } = "";
    public bool Sample { get; set; }
}

public class PlanReminderDeliveryDto
{
    public Guid Id { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public string DepartmentName { get; set; } = "";
    public string RecipientEmail { get; set; } = "";
    public int Year { get; set; }
    public int Month { get; set; }
    public string Subject { get; set; } = "";
    public string MessageType { get; set; } = "";
    public int Attempts { get; set; }
    public string Status { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public DateTime? LastAttemptAt { get; set; }
    public string? FailureDetail { get; set; }
}

public class CeoInvitationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? JobTitle { get; set; }
    public bool ActivateOnAccept { get; set; }
    public DateTime ExpiresAt { get; set; }
}
