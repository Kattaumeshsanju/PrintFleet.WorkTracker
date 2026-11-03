using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public sealed record WorkCalendarConfig(IReadOnlySet<DayOfWeek> WeeklyOffs, string AlternateSaturday);

public sealed record AttendanceMath(int Present, int Leave, int CompOff, int Od, int Absent, int Unmarked, int TotalWorked, int AvailableWorkingDays);

public static class AttendanceRules
{
    public static WorkCalendarConfig Default { get; } = new(new HashSet<DayOfWeek> { DayOfWeek.Sunday }, AlternateSaturdayRules.SecondAndFourth);

    public static WorkCalendarConfig Parse(string? weeklyOffs, string? alternateSaturday)
    {
        var days = (weeklyOffs ?? "0")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var day) && day is >= 0 and <= 6 ? (DayOfWeek)day : (DayOfWeek?)null)
            .Where(day => day is not null)
            .Select(day => day!.Value)
            .ToHashSet();
        if (days.Count == 0) days.Add(DayOfWeek.Sunday);
        var rule = AlternateSaturdayRules.All.Contains(alternateSaturday) ? alternateSaturday! : AlternateSaturdayRules.SecondAndFourth;
        return new WorkCalendarConfig(days, rule);
    }

    public static string FormatOffs(IEnumerable<DayOfWeek> days)
        => string.Join(",", days.Select(day => (int)day).Distinct().Order());

    public static int SaturdayNumber(DateOnly date)
    {
        if (date.DayOfWeek != DayOfWeek.Saturday) return 0;
        var count = 0;
        for (var day = 1; day <= date.Day; day++)
            if (new DateOnly(date.Year, date.Month, day).DayOfWeek == DayOfWeek.Saturday) count++;
        return count;
    }

    public static bool IsAlternateSaturdayOff(DateOnly date, string rule)
    {
        if (date.DayOfWeek != DayOfWeek.Saturday) return false;
        var number = SaturdayNumber(date);
        return rule switch
        {
            AlternateSaturdayRules.AllOff => true,
            AlternateSaturdayRules.SecondAndFourth => number is 2 or 4,
            AlternateSaturdayRules.FirstAndThird => number is 1 or 3,
            _ => false
        };
    }

    public static bool IsWorkingDay(DateOnly date, WorkCalendarConfig calendar, IReadOnlySet<DateOnly> holidays)
    {
        if (holidays.Contains(date)) return false;
        if (calendar.WeeklyOffs.Contains(date.DayOfWeek)) return false;
        return !IsAlternateSaturdayOff(date, calendar.AlternateSaturday);
    }

    public static List<DateOnly> DaysInMonth(int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        var days = new List<DateOnly>();
        for (var date = start; date.Month == month; date = date.AddDays(1)) days.Add(date);
        return days;
    }

    public static List<DateOnly> WorkingDays(int year, int month, WorkCalendarConfig calendar, IReadOnlySet<DateOnly> holidays)
        => DaysInMonth(year, month).Where(date => IsWorkingDay(date, calendar, holidays)).ToList();

    public static List<DateOnly> WorkingDaysInRange(DateOnly start, DateOnly end, WorkCalendarConfig calendar, IReadOnlySet<DateOnly> holidays)
    {
        if (end < start) throw new AppException("The end date must be on or after the start date.");
        if (end.DayNumber - start.DayNumber > 62) throw new AppException("A leave or duty range cannot be longer than 62 days.");
        var days = new List<DateOnly>();
        for (var date = start; date <= end; date = date.AddDays(1))
            if (IsWorkingDay(date, calendar, holidays)) days.Add(date);
        return days;
    }

    public static AttendanceMath Summarize(IReadOnlyCollection<DateOnly> workingDays, IEnumerable<(DateOnly Date, string Status)> marks)
    {
        var working = workingDays.ToHashSet();
        var byDate = new Dictionary<DateOnly, string>();
        foreach (var mark in marks)
        {
            if (byDate.ContainsKey(mark.Date))
                throw new AppException("An employee cannot have two attendance statuses on the same date.");
            byDate[mark.Date] = mark.Status;
        }

        var present = byDate.Count(item => item.Value == AttendanceStatuses.Present);
        var leave = byDate.Count(item => item.Value == AttendanceStatuses.Leave && working.Contains(item.Key));
        var comp = byDate.Count(item => item.Value == AttendanceStatuses.CompOff && working.Contains(item.Key));
        var od = byDate.Count(item => item.Value == AttendanceStatuses.Od && working.Contains(item.Key));
        var absent = byDate.Count(item => item.Value == AttendanceStatuses.Absent && working.Contains(item.Key));
        var marked = byDate.Keys.Where(working.Contains).ToHashSet();
        var unmarked = working.Count(date => !marked.Contains(date));
        return new AttendanceMath(present, leave, comp, od, absent, unmarked, present + od, working.Count);
    }

    public static (DateOnly? From, DateOnly? To) Span(IEnumerable<(DateOnly Date, string Status)> marks, params string[] statuses)
    {
        var dates = marks.Where(mark => statuses.Contains(mark.Status)).Select(mark => mark.Date).Order().ToList();
        return dates.Count == 0 ? (null, null) : (dates[0], dates[^1]);
    }

    public static bool RegisterIsLocked(string status, bool actorIsAdmin)
        => status == AttendanceRegisterStatuses.Approved || (status == AttendanceRegisterStatuses.Submitted && !actorIsAdmin);

    public static bool CanEdit(string role) => role is Roles.Admin or Roles.TeamLead;

    public static bool CanReview(string role) => role == Roles.Admin;

    public static void EnsureDepartment(string role, Guid? ownDepartment, Guid requested)
    {
        if (role is Roles.Admin or Roles.Ceo) return;
        if (role == Roles.TeamLead && ownDepartment == requested) return;
        throw new AppException("You can open attendance only for your own department.", StatusCodes.Status403Forbidden);
    }
}
