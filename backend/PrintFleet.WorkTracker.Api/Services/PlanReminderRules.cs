using System.Net;
using System.Net.Mail;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public sealed record PlanMember(Guid UserId, string Name, string Email, string Role, Guid DepartmentId, string DepartmentName, bool UserActive, bool DepartmentActive);

public sealed record PlanGoalLine(string Title, string? Description, string? Project, string Status, int ProgressPercent, DateOnly Start, DateOnly End, string? Remarks, decimal EstimatedHours);

public static class PlanReminderRules
{
    public const int MaxAttempts = 3;
    public static readonly Guid SettingsId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    public static TimeZoneInfo IndiaTime()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
    }

    public static bool IsValidEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var email = value.Trim();
        if (email.Length > 200 || email.Any(char.IsWhiteSpace) || email.Contains('\n')) return false;
        try
        {
            var parsed = new MailAddress(email);
            return parsed.Address.Equals(email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static IReadOnlyList<PlanMember> PersonalRecipients(IEnumerable<PlanMember> people)
    {
        return people
            .Where(person => person.UserActive && person.DepartmentActive)
            .Where(person => person.Role is Roles.TeamLead or Roles.TeamMember)
            .Where(person => IsValidEmail(person.Email))
            .GroupBy(person => person.UserId)
            .Select(group => group.First())
            .OrderBy(person => person.DepartmentName).ThenBy(person => person.Name)
            .ToList();
    }

    public static IReadOnlyList<PlanMember> SummaryRecipients(IEnumerable<PlanMember> people)
    {
        return PersonalRecipients(people)
            .Where(person => person.Role == Roles.TeamLead)
            .GroupBy(person => person.DepartmentId)
            .Select(group => group.First())
            .ToList();
    }

    public static string LeadName(IEnumerable<PlanMember> departmentPeople)
        => PersonalRecipients(departmentPeople).FirstOrDefault(person => person.Role == Roles.TeamLead)?.Name ?? "Not assigned";

    public static bool IsDue(DateTime local, int hour, int minute, string weekdays)
    {
        var allowed = ParseWeekdays(weekdays);
        if (!allowed.Contains((int)local.DayOfWeek)) return false;
        return local.TimeOfDay >= new TimeSpan(Math.Clamp(hour, 0, 23), Math.Clamp(minute, 0, 59), 0);
    }

    public static HashSet<int> ParseWeekdays(string? value)
    {
        var days = (value ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var day) ? day : -1)
            .Where(day => day is >= 0 and <= 6)
            .ToHashSet();
        return days.Count == 0 ? [1, 2, 3, 4, 5] : days;
    }

    public static string FormatWeekdays(IEnumerable<int> days)
        => string.Join(",", days.Where(day => day is >= 0 and <= 6).Distinct().Order());

    public static string? MailTransport(bool graphReady, bool smtpReady)
        => graphReady ? "Graph" : smtpReady ? "Smtp" : null;

    public static string AcceptanceLabel(string? transport)
        => transport == "Smtp" ? "Accepted by SMTP" : "Accepted by Microsoft Graph";

    public static string Key(DateOnly day, string messageType, Guid userId, int year, int month)
        => $"{day:yyyy-MM-dd}|{messageType}|{userId:N}|{year:D4}{month:D2}";

    public static bool MayAttempt(string status, bool retryable, int attempts, DateTime? nextAttemptAt, DateTime utcNow)
    {
        if (status == PlanReminderStatuses.Pending) return true;
        if (status != PlanReminderStatuses.Failed || !retryable || attempts >= MaxAttempts) return false;
        return nextAttemptAt is null || nextAttemptAt <= utcNow;
    }

    public static bool CountsAsActive(string status) => ExecutiveMetrics.CountsTowardPlan(status);

    public static bool IsOverdue(PlanGoalLine goal, DateOnly today)
        => ExecutiveMetrics.IsOverdue(goal.Status, goal.End, today);

    public static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");

    public static string CleanSubject(string value)
    {
        var cleaned = new string(value.Select(character => char.IsControl(character) ? ' ' : character).ToArray()).Trim();
        return cleaned.Length <= 180 ? cleaned : cleaned[..180];
    }
}
