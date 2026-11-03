using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public static class DepartmentRules
{
    public static bool CanViewDaily(Guid actorId, string actorRole, Guid targetId)
        => actorId == targetId || actorRole is Roles.Admin or Roles.Ceo;

    public static bool CanViewOrganization(string role)
        => role is Roles.Admin or Roles.Ceo;

    public static bool CanEditDepartmentWork(string role)
        => role is Roles.Admin or Roles.TeamLead;

    public static bool CanAssignRole(bool isAdmin, string role)
        => role == Roles.TeamMember || (isAdmin && role == Roles.TeamLead);

    public static bool CanMoveIntoDepartment(bool isAdmin, Guid? actorDepartmentId, Guid? personDepartmentId, Guid targetDepartmentId)
    {
        if (isAdmin) return true;
        if (actorDepartmentId != targetDepartmentId) return false;
        return personDepartmentId is null || personDepartmentId == targetDepartmentId;
    }

    public static bool CanDeactivateDepartment(int activeUsers, int activeProjects)
        => activeUsers == 0 && activeProjects == 0;

    public static bool HasSingleOrNoLead(IEnumerable<string> roles)
        => roles.Count(role => role == Roles.TeamLead) <= 1;

    public static bool CanChangeTeamLead(bool isAdmin, Guid? currentLeadId, Guid? requestedLeadId)
        => isAdmin || currentLeadId == requestedLeadId;
}

public static class AccessRules
{
    public static bool Overview(string role, bool granted)
        => role is Roles.Admin or Roles.Ceo || granted;

    public static bool Reports(string role, bool granted)
        => role is Roles.Admin or Roles.Ceo || granted;

    public static bool Research(string role)
        => role is Roles.Admin or Roles.Ceo;

    public static bool CanReceiveGrant(string role)
        => role is Roles.TeamLead or Roles.TeamMember;

    public static bool CanManageAccess(string role)
        => role == Roles.Admin;
}

public static class ExecutiveMetrics
{
    public const int DefaultStaleProgressDays = 14;

    public static bool CountsTowardPlan(string status) => status != TaskStatuses.Cancelled;

    public static bool IsOverdue(string status, DateOnly due, DateOnly today)
        => due < today && TaskStatuses.Open.Contains(status);

    public static decimal? Completion(int completed, int counted)
        => counted == 0 ? null : WorkMetrics.Completion(completed, counted);

    public static bool ProjectAtRisk(string status, DateOnly? target, DateOnly today, bool hasBlockedGoal, bool hasOverdueGoal)
        => status is ProjectStatuses.Active or ProjectStatuses.OnHold
           && ((target is DateOnly due && due < today) || hasBlockedGoal || hasOverdueGoal);

    public static bool IsStale(DateTime updatedAtUtc, DateTime utcNow, int staleDays)
        => staleDays > 0 && updatedAtUtc < utcNow.AddDays(-staleDays);
}
