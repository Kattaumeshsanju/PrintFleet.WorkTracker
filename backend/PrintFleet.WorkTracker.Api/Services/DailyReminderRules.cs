using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public readonly record struct DailyReminderCandidate(
    Guid UserId,
    string Email,
    string Role,
    bool UserActive,
    bool DepartmentActive,
    bool DepartmentEnabled);

public static class DailyReminderRules
{
    public static bool ShouldReceive(DailyReminderCandidate person, string? submissionStatus, string? dayType)
    {
        if (!person.UserActive || !person.DepartmentActive || !person.DepartmentEnabled) return false;
        if (person.Role is not (Roles.TeamLead or Roles.TeamMember)) return false;
        if (!PlanReminderRules.IsValidEmail(person.Email)) return false;
        if (submissionStatus == SubmissionStatuses.Submitted) return false;
        return dayType is not (DayTypes.Holiday or DayTypes.Leave);
    }

    public static IReadOnlyList<DailyReminderCandidate> Select(
        IEnumerable<DailyReminderCandidate> people,
        Func<Guid, string?> submissionStatus,
        Func<Guid, string?> dayType)
    {
        return people
            .Where(person => ShouldReceive(person, submissionStatus(person.UserId), dayType(person.UserId)))
            .GroupBy(person => person.UserId)
            .Select(group => group.First())
            .OrderBy(person => person.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
