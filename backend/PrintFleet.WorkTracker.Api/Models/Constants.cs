namespace PrintFleet.WorkTracker.Api.Models;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Ceo = "CEO";
    public const string TeamLead = "TeamLead";
    public const string TeamMember = "TeamMember";
    public const string Managers = "TeamLead,Admin";
    public const string Readers = "TeamLead,Admin,CEO";
    public const string Organization = "Admin,CEO";
    public static readonly string[] All = [Admin, Ceo, TeamLead, TeamMember];
}

public static class OrganizationPermissions
{
    public const string Overview = "OrganizationOverview";
    public const string Reports = "OrganizationReports";
    public static readonly string[] Assignable = [Overview, Reports];
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

public static class DepartmentPlanStatuses
{
    public const string Open = "Open";
    public const string InReview = "InReview";
    public const string Closed = "Closed";
    public static readonly string[] All = [Open, InReview, Closed];
}

public static class DayTypes
{
    public const string Holiday = "Holiday";
    public const string Leave = "Leave";
    public static readonly string[] All = [Holiday, Leave];
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

public static class InitiativeStatuses
{
    public const string Planned = "Planned";
    public const string Ongoing = "Ongoing";
    public const string Completed = "Completed";
    public const string OnHold = "OnHold";
    public const string Cancelled = "Cancelled";
    public static readonly string[] All = [Planned, Ongoing, Completed, OnHold, Cancelled];
    public static readonly string[] Open = [Planned, Ongoing, OnHold];
}

public static class ResearchStages
{
    public const string Idea = "Idea";
    public const string Research = "Research";
    public const string ProofOfConcept = "ProofOfConcept";
    public const string Validation = "Validation";
    public const string Pilot = "Pilot";
    public const string Completed = "Completed";
    public static readonly string[] All = [Idea, Research, ProofOfConcept, Validation, Pilot, Completed];
}

public static class InvitationStatuses
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Revoked = "Revoked";
    public const string Expired = "Expired";
}

public static class NoticeCategories
{
    public const string General = "General";
    public const string Project = "Project";
    public const string Plan = "Plan";
    public const string Deadline = "Deadline";
    public const string Meeting = "Meeting";
    public const string Blocker = "Blocker";
    public static readonly string[] All = [General, Project, Plan, Deadline, Meeting, Blocker];
}

public static class NoticeChannels
{
    public const string Email = "Email";
    public const string InApp = "InApp";
    public const string Both = "Both";
    public static readonly string[] All = [Email, InApp, Both];
}

public static class EmailDeliveryStatuses
{
    public const string Pending = "Pending";
    public const string SentToProvider = "SentToProvider";
    public const string Failed = "Failed";
    public const string Bounced = "Bounced";
}

public static class PlanReminderTypes
{
    public const string PersonalPlan = "PersonalPlan";
    public const string MissingPlan = "MissingPlan";
    public const string DepartmentSummary = "DepartmentSummary";
    public const string Test = "Test";
}

public static class PlanReminderStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Accepted = "Accepted";
    public const string Failed = "Failed";
}

public static class PlanReminderRunStatuses
{
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
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
