using System.Text.RegularExpressions;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public static class CategoryRules
{
    public static string Normalize(string? name)
        => Regex.Replace((name ?? "").Trim(), @"\s+", " ");

    public static string Key(string? name)
        => Normalize(name).ToLowerInvariant();

    public static bool CanManage(string role, Guid? actorDepartmentId, Guid targetDepartmentId)
        => role == Roles.Admin || (role == Roles.TeamLead && actorDepartmentId == targetDepartmentId);

    public static bool CanAssign(Guid categoryDepartmentId, Guid ownerDepartmentId, bool categoryActive, bool alreadyAssigned)
        => categoryDepartmentId == ownerDepartmentId && (categoryActive || alreadyAssigned);

    public static bool CanReassign(Guid sourceDepartmentId, Guid targetDepartmentId)
        => sourceDepartmentId == targetDepartmentId;

    public static decimal? Completion(int completed, int counted)
        => counted == 0 ? null : Math.Round(completed * 100m / counted, 1);

    public static string NextNumber(IEnumerable<string> existing)
    {
        var max = 0;
        foreach (var value in existing)
        {
            if (value.StartsWith("RD-", StringComparison.Ordinal) && int.TryParse(value[3..], out var number))
                max = Math.Max(max, number);
        }
        return $"RD-{(max + 1):000}";
    }

    public static bool CanEditInitiative(string role, Guid? actorDepartmentId, Guid initiativeDepartmentId)
        => role == Roles.Admin || (role == Roles.TeamLead && actorDepartmentId == initiativeDepartmentId);

    public static bool CanAddResearch(string role, Guid? actorDepartmentId, Guid initiativeDepartmentId, bool isOwnerOrCollaborator)
        => CanEditInitiative(role, actorDepartmentId, initiativeDepartmentId)
            || (role == Roles.TeamMember && isOwnerOrCollaborator && actorDepartmentId == initiativeDepartmentId);

    public static (Guid? ProjectId, Guid? CategoryId) LinkGoal(Guid? requestedProjectId, Guid? initiativeProjectId, Guid? requestedCategoryId, Guid? initiativeCategoryId)
    {
        if (requestedProjectId is Guid chosen && initiativeProjectId is Guid linked && chosen != linked)
            throw new AppException("That initiative is linked to a different project.");
        if (requestedCategoryId is Guid category && initiativeCategoryId is Guid initiativeCategory && category != initiativeCategory)
            throw new AppException("That initiative uses a different work category.");
        return (requestedProjectId ?? initiativeProjectId, requestedCategoryId ?? initiativeCategoryId);
    }
}
