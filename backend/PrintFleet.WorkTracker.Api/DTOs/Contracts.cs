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
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
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
    public Guid? DepartmentId { get; set; }
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
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Priority { get; set; } = "";
    public DateOnly PlannedStartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }
    public decimal EstimatedHours { get; set; }
    public decimal ActualHours { get; set; }
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
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Priority { get; set; } = Priorities.Medium;
    public DateOnly PlannedStartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }
    public decimal EstimatedHours { get; set; }
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
    public string Severity { get; set; } = "info";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Href { get; set; }
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

public class TeamPlansDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public IReadOnlyList<EmployeeOverviewDto> Employees { get; set; } = [];
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
