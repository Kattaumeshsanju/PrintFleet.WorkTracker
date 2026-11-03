using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public readonly record struct GoalProgress(int ProgressPercent, DateOnly? ActualStart, DateOnly? ActualEnd);

public static class WorkRecords
{
    public static Guid? ReportingDepartment(Guid? sheetDepartmentId, Guid? currentUserDepartmentId)
        => sheetDepartmentId ?? currentUserDepartmentId;

    public static bool IsDuplicateGoal(string title, Guid? projectId, IEnumerable<(string Title, Guid? ProjectId, string Status)> existing)
    {
        var key = title.Trim();
        return existing.Any(item =>
            item.Status != TaskStatuses.Cancelled
            && item.ProjectId == projectId
            && string.Equals(item.Title.Trim(), key, StringComparison.OrdinalIgnoreCase));
    }

    public static bool CanAssignProject(string projectStatus)
        => projectStatus is ProjectStatuses.Active or ProjectStatuses.OnHold;

    public static bool CanChangeProjectDepartment(bool hasMonthlyGoals) => !hasMonthlyGoals;

    public static decimal? PlanCompletion(IEnumerable<(string Status, int ProgressPercent, decimal EstimatedHours)> goals)
    {
        var counted = new List<(int Progress, decimal Hours)>();
        foreach (var goal in goals)
        {
            if (!ExecutiveMetrics.CountsTowardPlan(goal.Status)) continue;
            var progress = goal.Status == TaskStatuses.Completed ? 100 : Math.Clamp(goal.ProgressPercent, 0, 100);
            counted.Add((progress, goal.EstimatedHours < 0 ? 0 : goal.EstimatedHours));
        }

        if (counted.Count == 0) return null;
        var hours = counted.Sum(item => item.Hours);
        if (hours > 0)
            return Math.Round(counted.Sum(item => item.Hours * item.Progress) / hours, 1, MidpointRounding.AwayFromZero);
        return Math.Round(counted.Average(item => (decimal)item.Progress), 1, MidpointRounding.AwayFromZero);
    }

    public static GoalProgress ApplyGoalProgress(string previousStatus, string requestedStatus, int requestedProgress, DateOnly? actualStart, DateOnly? actualEnd, DateOnly today)
    {
        var progress = requestedStatus == TaskStatuses.Completed ? 100 : Math.Clamp(requestedProgress, 0, 100);
        var start = actualStart;
        var end = actualEnd;
        if (requestedStatus is TaskStatuses.InProgress or TaskStatuses.Blocked or TaskStatuses.Completed)
            start ??= today;
        if (requestedStatus == TaskStatuses.Completed)
            end ??= today;
        else if (previousStatus == TaskStatuses.Completed)
            end = null;
        return new GoalProgress(progress, start, end);
    }
}

public static class ReminderLedger
{
    public static IReadOnlyList<string> FailedAddresses(string? details)
    {
        if (string.IsNullOrWhiteSpace(details)) return [];
        const string marker = "failed addresses:";
        var index = details.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return [];
        return details[(index + marker.Length)..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static string Describe(DateOnly day, int sent, int skipped, IReadOnlyList<string> failed)
    {
        var text = $"{day:yyyy-MM-dd} sent {sent}; skipped {skipped}; failed {failed.Count}";
        return failed.Count == 0 ? text : $"{text}; failed addresses: {string.Join(", ", failed)}";
    }
}
