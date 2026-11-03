namespace PrintFleet.WorkTracker.Api.Models;

public class Department
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool DailyReminderEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}

public class User
{
    public Guid Id { get; set; }
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.TeamMember;
    public string? JobTitle { get; set; }
    public string? EmployeeNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<MonthlyWorkSheet> Sheets { get; set; } = new List<MonthlyWorkSheet>();
    public ICollection<DailyWorkEntry> DailyEntries { get; set; } = new List<DailyWorkEntry>();
    public ICollection<DailySubmission> Submissions { get; set; } = new List<DailySubmission>();
    public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();
}

public class UserPermission
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Permission { get; set; } = "";
    public Guid GrantedById { get; set; }
    public DateTime GrantedAt { get; set; }
}

public class Project
{
    public Guid Id { get; set; }
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Priority { get; set; } = Priorities.Medium;
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; } = ProjectStatuses.Active;
    public Guid? WorkCategoryId { get; set; }
    public WorkCategory? WorkCategory { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<MonthlyTask> Tasks { get; set; } = new List<MonthlyTask>();
}

public class DepartmentMonthlyPlan
{
    public Guid Id { get; set; }
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public string Status { get; set; } = DepartmentPlanStatuses.Open;
    public string? Summary { get; set; }
    public string? Risks { get; set; }
    public string? Comments { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<MonthlyWorkSheet> Sheets { get; set; } = new List<MonthlyWorkSheet>();
}

public class MonthlyWorkSheet
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid? DepartmentPlanId { get; set; }
    public DepartmentMonthlyPlan? DepartmentPlan { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Status { get; set; } = SheetStatuses.Open;
    public DateTime? LockedAt { get; set; }
    public Guid? LockedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<MonthlyTask> Tasks { get; set; } = new List<MonthlyTask>();
}

public class MonthlyTask
{
    public Guid Id { get; set; }
    public Guid MonthlyWorkSheetId { get; set; }
    public MonthlyWorkSheet Sheet { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? WorkCategoryId { get; set; }
    public WorkCategory? WorkCategory { get; set; }
    public Guid? InitiativeId { get; set; }
    public Initiative? Initiative { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Priority { get; set; } = Priorities.Medium;
    public DateOnly PlannedStartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    public decimal EstimatedHours { get; set; }
    public int ProgressPercent { get; set; }
    public string Status { get; set; } = TaskStatuses.Planned;
    public string? Remarks { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<DailyWorkEntry> DailyEntries { get; set; } = new List<DailyWorkEntry>();
}

public class DailyWorkEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid MonthlyTaskId { get; set; }
    public MonthlyTask MonthlyTask { get; set; } = null!;
    public DateOnly WorkDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public string Description { get; set; } = "";
    public string Status { get; set; } = EntryStatuses.InProgress;
    public string? Remarks { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DailySubmission
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateOnly WorkDate { get; set; }
    public string? DayType { get; set; }
    public string Status { get; set; } = SubmissionStatuses.Pending;
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedBy { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public Guid? ReopenedBy { get; set; }
}

public class WorkCategory
{
    public Guid Id { get; set; }
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public string Name { get; set; } = "";
    public string NameKey { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Initiative
{
    public Guid Id { get; set; }
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public Guid? WorkCategoryId { get; set; }
    public WorkCategory? WorkCategory { get; set; }
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Number { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Status { get; set; } = InitiativeStatuses.Planned;
    public string Priority { get; set; } = Priorities.Medium;
    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }
    public DateOnly? TargetStart { get; set; }
    public DateOnly? TargetEnd { get; set; }
    public string? Remarks { get; set; }
    public string? ResearchObjective { get; set; }
    public string? Hypothesis { get; set; }
    public string? ResearchStage { get; set; }
    public string? SuccessCriteria { get; set; }
    public string? ExperimentPlan { get; set; }
    public string? Conclusion { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<InitiativeCollaborator> Collaborators { get; set; } = new List<InitiativeCollaborator>();
    public ICollection<InitiativeMilestone> Milestones { get; set; } = new List<InitiativeMilestone>();
    public ICollection<ResearchRecord> Records { get; set; } = new List<ResearchRecord>();
}

public class InitiativeCollaborator
{
    public Guid InitiativeId { get; set; }
    public Initiative Initiative { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}

public class InitiativeMilestone
{
    public Guid Id { get; set; }
    public Guid InitiativeId { get; set; }
    public Initiative Initiative { get; set; } = null!;
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }
    public DateOnly? TargetDate { get; set; }
    public string Status { get; set; } = InitiativeStatuses.Planned;
    public DateOnly? CompletedOn { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ResearchRecord
{
    public Guid Id { get; set; }
    public Guid InitiativeId { get; set; }
    public Initiative Initiative { get; set; } = null!;
    public string Title { get; set; } = "";
    public DateOnly RecordedOn { get; set; }
    public string? Objective { get; set; }
    public string? Method { get; set; }
    public string? Findings { get; set; }
    public string? Outcome { get; set; }
    public string? EvidenceLinks { get; set; }
    public string? NextSteps { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CeoInvitation
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? JobTitle { get; set; }
    public bool ActivateOnAccept { get; set; } = true;
    public string TokenHash { get; set; } = "";
    public string Status { get; set; } = InvitationStatuses.Pending;
    public Guid InvitedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class TeamInvitation
{
    public Guid Id { get; set; }
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? JobTitle { get; set; }
    public string TokenHash { get; set; } = "";
    public string Status { get; set; } = InvitationStatuses.Pending;
    public Guid InvitedBy { get; set; }
    public Guid? AcceptedUserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TeamNotice
{
    public Guid Id { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Category { get; set; } = NoticeCategories.General;
    public string Channel { get; set; } = NoticeChannels.Both;
    public Guid? ProjectId { get; set; }
    public string? LinkPath { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<TeamNoticeRecipient> Recipients { get; set; } = new List<TeamNoticeRecipient>();
}

public class TeamNoticeRecipient
{
    public Guid Id { get; set; }
    public Guid NoticeId { get; set; }
    public TeamNotice Notice { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Email { get; set; } = "";
    public DateTime? ReadAt { get; set; }
    public EmailDelivery? Delivery { get; set; }
}

public class EmailDelivery
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = EmailDeliveryKinds.TeamNotice;
    public Guid? RecipientId { get; set; }
    public TeamNoticeRecipient? Recipient { get; set; }
    public Guid? UserId { get; set; }
    public string? RecipientEmail { get; set; }
    public string? Subject { get; set; }
    public DateOnly? WorkDate { get; set; }
    public string Status { get; set; } = EmailDeliveryStatuses.Pending;
    public int Attempts { get; set; }
    public string? ErrorType { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PlanReminderSettings
{
    public Guid Id { get; set; }
    public bool Enabled { get; set; }
    public bool MissingPlanEnabled { get; set; } = true;
    public bool DepartmentSummaryEnabled { get; set; }
    public int Hour { get; set; } = 17;
    public int Minute { get; set; }
    public string Weekdays { get; set; } = "1,2,3,4,5";
    public DateTime UpdatedAt { get; set; }
}

public class PlanReminderRun
{
    public Guid Id { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = PlanReminderRunStatuses.Running;
    public int AcceptedCount { get; set; }
    public int FailedCount { get; set; }
    public string? FailureDetail { get; set; }
}

public class PlanReminderDelivery
{
    public Guid Id { get; set; }
    public Guid? RunId { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public Guid? DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public Guid UserId { get; set; }
    public string RecipientEmail { get; set; } = "";
    public int Year { get; set; }
    public int Month { get; set; }
    public string Subject { get; set; } = "";
    public string MessageType { get; set; } = PlanReminderTypes.PersonalPlan;
    public string? Transport { get; set; }
    public int Attempts { get; set; }
    public string Status { get; set; } = PlanReminderStatuses.Pending;
    public bool Retryable { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? FailureDetail { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DepartmentWorkCalendar
{
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public string WeeklyOffs { get; set; } = "0";
    public string AlternateSaturday { get; set; } = AlternateSaturdayRules.SecondAndFourth;
}

public class CompanyHoliday
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateOnly HolidayDate { get; set; }
    public string HolidayType { get; set; } = "Company";
    public Guid? DepartmentId { get; set; }
}

public class AttendanceDay
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid DepartmentId { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public string Status { get; set; } = AttendanceStatuses.Present;
    public string? Remarks { get; set; }
    public string? LeaveType { get; set; }
    public DateOnly? EarnedDate { get; set; }
    public Guid? SpanId { get; set; }
    public Guid RecordedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AttendanceSpan
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DepartmentId { get; set; }
    public string Kind { get; set; } = AttendanceStatuses.Leave;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? LeaveType { get; set; }
    public DateOnly? EarnedDate { get; set; }
    public string? Remarks { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MonthlyAttendanceRegister
{
    public Guid Id { get; set; }
    public Guid DepartmentId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Status { get; set; } = AttendanceRegisterStatuses.Draft;
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewReason { get; set; }
    public DateOnly PreparedOn { get; set; }
}

public class AttendanceChange
{
    public Guid Id { get; set; }
    public Guid? AttendanceDayId { get; set; }
    public Guid? RegisterId { get; set; }
    public Guid UserId { get; set; }
    public string Action { get; set; } = "";
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public Guid ChangedById { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class AttendanceCorrection
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid DepartmentId { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public string RequestedStatus { get; set; } = AttendanceStatuses.Present;
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public Guid? DecidedById { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditLog
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
