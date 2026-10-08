namespace PrintFleet.WorkTracker.Api.Models;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.TeamMember;
    public string? JobTitle { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<MonthlyWorkSheet> Sheets { get; set; } = new List<MonthlyWorkSheet>();
    public ICollection<DailyWorkEntry> DailyEntries { get; set; } = new List<DailyWorkEntry>();
    public ICollection<DailySubmission> Submissions { get; set; } = new List<DailySubmission>();
}

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; } = ProjectStatuses.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<MonthlyTask> Tasks { get; set; } = new List<MonthlyTask>();
}

public class MonthlyWorkSheet
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
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
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Priority { get; set; } = Priorities.Medium;
    public DateOnly PlannedStartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }
    public decimal EstimatedHours { get; set; }
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
    public string Status { get; set; } = SubmissionStatuses.Pending;
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedBy { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public Guid? ReopenedBy { get; set; }
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
