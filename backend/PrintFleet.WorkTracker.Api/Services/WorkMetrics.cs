using System.Globalization;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public static class WorkMetrics
{
    public static TimeOnly ParseTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new AppException("Start and end times are required.");

        var formats = new[] { "HH:mm", "H:mm", "HH:mm:ss" };
        if (TimeOnly.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            return time;

        throw new AppException("Enter times as HH:mm.");
    }

    public static string FormatTime(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static int DurationMinutes(TimeOnly start, TimeOnly end)
    {
        if (end <= start)
            throw new AppException("End time must be after start time.");

        return (int)(end.ToTimeSpan() - start.ToTimeSpan()).TotalMinutes;
    }

    public static bool Overlaps(TimeOnly start, TimeOnly end, TimeOnly otherStart, TimeOnly otherEnd)
        => start < otherEnd && end > otherStart;

    public static decimal Hours(int minutes) => Math.Round(minutes / 60m, 2, MidpointRounding.AwayFromZero);

    public static decimal Completion(int completed, int total)
        => total == 0 ? 0 : Math.Round(completed * 100m / total, 1, MidpointRounding.AwayFromZero);

    public static string FormatMinutes(int minutes)
    {
        if (minutes < 0) minutes = 0;
        var hours = minutes / 60;
        var remainder = minutes % 60;
        if (hours == 0) return $"{remainder}m";
        if (remainder == 0) return $"{hours}h";
        return $"{hours}h {remainder}m";
    }

    public static bool IsWeekend(DateOnly date)
        => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    public static void Require(bool condition, string message, int statusCode = StatusCodes.Status400BadRequest)
    {
        if (!condition) throw new AppException(message, statusCode);
    }

    public static void RequireOneOf(string value, IEnumerable<string> allowed, string message)
    {
        if (!allowed.Contains(value)) throw new AppException(message);
    }
}
