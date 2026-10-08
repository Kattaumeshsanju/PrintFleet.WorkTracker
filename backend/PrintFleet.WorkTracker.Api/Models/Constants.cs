namespace PrintFleet.WorkTracker.Api.Models;

public static class Roles
{
    public const string TeamLead = "TeamLead";
    public const string TeamMember = "TeamMember";
    public static readonly string[] All = [TeamLead, TeamMember];
}

public static class TaskStatuses
{
    public const string Planned = "Planned";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Blocked = "Blocked";
    public const string Cancelled = "Cancelled";
    public static readonly string[] All = [Planned, InProgress, Completed, Blocked, Cancelled];
    public static readonly string[] Open = [Planned, InProgress, Blocked];
}

public static class EntryStatuses
{
    public const string Completed = "Completed";
    public const string InProgress = "InProgress";
    public const string Blocked = "Blocked";
    public static readonly string[] All = [Completed, InProgress, Blocked];
}

public static class Priorities
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";
    public static readonly string[] All = [Low, Medium, High, Critical];
}

public static class SheetStatuses
{
    public const string Open = "Open";
    public const string Locked = "Locked";
}

public static class SubmissionStatuses
{
    public const string Pending = "Pending";
    public const string Submitted = "Submitted";
}

public static class ProjectStatuses
{
    public const string Active = "Active";
    public const string OnHold = "OnHold";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public static readonly string[] All = [Active, OnHold, Completed, Cancelled];
}

public static class AuditActions
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";
    public const string Submitted = "Submitted";
    public const string Reopened = "Reopened";
    public const string Locked = "Locked";
    public const string Unlocked = "Unlocked";
}
