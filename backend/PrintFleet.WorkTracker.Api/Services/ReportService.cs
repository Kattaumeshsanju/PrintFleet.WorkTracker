using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IReportService
{
    Task<MonthlyReportDto> MonthlyAsync(int year, int month, Guid? userId, CancellationToken ct);
    Task<RangeReportDto> RangeAsync(DateOnly from, DateOnly to, Guid? userId, CancellationToken ct);
    Task<TeamReportDto> TeamAsync(int year, int month, bool includeSelf, Guid? departmentId, CancellationToken ct);
}

public class ReportService : IReportService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IMonthlyWorkService _monthly;
    private readonly IDailyWorkService _daily;
    private readonly IDepartmentAccess _access;

    public ReportService(AppDbContext db, ICurrentUser current, IMonthlyWorkService monthly, IDailyWorkService daily, IDepartmentAccess access)
    {
        _db = db;
        _current = current;
        _monthly = monthly;
        _daily = daily;
        _access = access;
    }

    public async Task<MonthlyReportDto> MonthlyAsync(int year, int month, Guid? userId, CancellationToken ct)
    {
        if (month is < 1 or > 12) throw new AppException("Month must be between 1 and 12.");
        var target = userId is null || userId == Guid.Empty || userId == _current.UserId ? _current.UserId : userId.Value;
        if (target != _current.UserId) await _access.EnsureCanManageUserAsync(target, ct);

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == target, ct)
            ?? throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);
        var sheet = await _monthly.FindSheetAsync(target, year, month, ct);
        var calendar = await _daily.GetCalendarAsync(year, month, target, ct);
        var tasks = sheet?.Tasks ?? [];
        var counts = UserService.CountTasks(tasks);
        var loggedDays = calendar.Days.Count(x => !x.IsWeekend && x.Minutes > 0);
        var actualHours = sheet?.ActualHours ?? 0;

        return new MonthlyReportDto
        {
            Employee = UserMapper.ToDto(user),
            Year = year,
            Month = month,
            SheetStatus = sheet?.Status ?? SheetStatuses.Open,
            PlannedTasks = counts.Total,
            Completed = counts.Completed,
            InProgress = counts.InProgress,
            Blocked = counts.Blocked,
            Cancelled = counts.Cancelled,
            Planned = counts.Planned,
            PlannedHours = counts.PlannedHours,
            ActualHours = actualHours,
            AverageDailyHours = loggedDays == 0 ? 0 : Math.Round(actualHours / loggedDays, 1, MidpointRounding.AwayFromZero),
            CompletionPercent = counts.Completion,
            DaysLogged = loggedDays,
            StatusDistribution = TaskStatuses.All.Select(status => new StatusCountDto
            {
                Status = status,
                Count = tasks.Count(x => x.Status == status)
            }).Where(x => x.Count > 0).ToList(),
            DailyHours = calendar.Days.Select(x => new DailyHoursPointDto
            {
                Date = x.Date,
                Minutes = x.Minutes,
                Submitted = x.Submitted,
                IsWeekend = x.IsWeekend
            }).ToList(),
            Tasks = tasks
        };
    }

    public async Task<RangeReportDto> RangeAsync(DateOnly from, DateOnly to, Guid? userId, CancellationToken ct)
    {
        if (from == default || to == default)
            throw new AppException("Choose a start date and an end date.");
        if (from > to)
            throw new AppException("Start date must be on or before the end date.");

        var target = userId is null || userId == Guid.Empty || userId == _current.UserId ? _current.UserId : userId.Value;
        if (target != _current.UserId) await _access.EnsureCanManageUserAsync(target, ct);

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == target, ct)
            ?? throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);
        var activities = await ActivitiesInRangeAsync(target, from, to, ct);
        var projects = activities
            .GroupBy(x => string.IsNullOrWhiteSpace(x.ProjectName) ? "No project" : x.ProjectName)
            .Select(group =>
            {
                var minutes = group.Sum(x => x.DurationMinutes);
                return new ProjectSummaryDto
                {
                    ProjectName = group.Key,
                    ActivityCount = group.Count(),
                    TotalMinutes = minutes,
                    TotalHours = WorkMetrics.Hours(minutes)
                };
            })
            .OrderBy(x => x.ProjectName)
            .ToList();
        var totalMinutes = activities.Sum(x => x.DurationMinutes);

        return new RangeReportDto
        {
            Employee = UserMapper.ToDto(user),
            From = from,
            To = to,
            ActivityCount = activities.Count,
            TotalMinutes = totalMinutes,
            TotalHours = WorkMetrics.Hours(totalMinutes),
            Projects = projects,
            Activities = activities
        };
    }

    private async Task<List<DailyEntryDto>> ActivitiesInRangeAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var entries = await _db.DailyWorkEntries.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.MonthlyTask).ThenInclude(x => x.Project)
            .Where(x => x.UserId == userId && x.WorkDate >= from && x.WorkDate <= to)
            .OrderBy(x => x.WorkDate).ThenBy(x => x.StartTime)
            .ToListAsync(ct);
        return entries.Select(DailyWorkService.MapEntry).ToList();
    }

    public async Task<TeamReportDto> TeamAsync(int year, int month, bool includeSelf, Guid? departmentId, CancellationToken ct)
    {
        _current.EnsureLead();
        if (month is < 1 or > 12) throw new AppException("Month must be between 1 and 12.");

        var plans = await _monthly.TeamAsync(year, month, null, null, null, null, null, departmentId, ct);
        var rowsSource = plans.Employees.Where(x => includeSelf || x.User.Id != _current.UserId).ToList();
        var userIds = rowsSource.Select(x => x.User.Id).ToArray();
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        var weekdays = ElapsedWeekdays(year, month);
        var daily = await _db.DailyWorkEntries.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) && x.WorkDate >= start && x.WorkDate < end)
            .GroupBy(x => new { x.UserId, x.WorkDate })
            .Select(g => new { g.Key.UserId, g.Key.WorkDate, Minutes = g.Sum(x => x.DurationMinutes) })
            .ToListAsync(ct);
        var submissions = await _db.DailySubmissions.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) && x.WorkDate >= start && x.WorkDate < end)
            .Select(x => new { x.UserId, x.WorkDate, x.Status, x.DayType })
            .ToListAsync(ct);
        var minutesByUser = daily.GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => g.ToDictionary(x => x.WorkDate, x => x.Minutes));
        var marksByUser = submissions.GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => g.ToDictionary(x => x.WorkDate));

        var rows = rowsSource.Select(x =>
        {
            minutesByUser.TryGetValue(x.User.Id, out var minutes);
            marksByUser.TryGetValue(x.User.Id, out var marks);
            var kpi = ScoreDays(weekdays, minutes, marks?.ToDictionary(pair => pair.Key, pair => new DayMark(pair.Value.Status, pair.Value.DayType)));
            return new TeamReportRowDto
            {
                UserId = x.User.Id,
                Name = x.User.Name,
                JobTitle = x.User.JobTitle,
                Role = x.User.Role,
                Planned = x.PlannedTasks,
                Completed = x.Completed,
                InProgress = x.InProgress,
                Blocked = x.Blocked,
                Cancelled = x.Cancelled,
                PlannedHours = x.PlannedHours,
                ActualHours = x.ActualHours,
                CompletionPercent = x.CompletionPercent,
                AvailableDays = kpi.Available,
                DaysLogged = kpi.Logged,
                DaysSubmitted = kpi.Submitted,
                MissingDays = kpi.Missing,
                LowHourDays = kpi.Low,
                LeaveDays = kpi.Leave,
                HolidayDays = kpi.Holiday,
                HoursAttainment = Rate(x.ActualHours, x.PlannedHours),
                UtilizationPercent = Rate(x.ActualHours, kpi.Available * ExpectedHoursPerDay),
                UpdateCompliance = Rate(kpi.Submitted, kpi.Available)
            };
        }).OrderBy(x => x.Name).ToList();

        var allTasks = rowsSource.SelectMany(x => x.Tasks).ToList();
        var completed = rows.Sum(x => x.Completed);
        var planned = rows.Sum(x => x.Planned);
        var plannedHours = rows.Sum(x => x.PlannedHours);
        var actualHours = rows.Sum(x => x.ActualHours);
        var availableDays = rows.Sum(x => x.AvailableDays);
        var blocked = rows.Sum(x => x.Blocked);
        return new TeamReportDto
        {
            Year = year,
            Month = month,
            IncludesSelf = includeSelf,
            PlannedTasks = planned,
            CompletedTasks = completed,
            PlannedHours = plannedHours,
            ActualHours = actualHours,
            CompletionPercent = WorkMetrics.Completion(completed, planned),
            AvailableDays = availableDays,
            DaysLogged = rows.Sum(x => x.DaysLogged),
            MissingDays = rows.Sum(x => x.MissingDays),
            LowHourDays = rows.Sum(x => x.LowHourDays),
            LeaveDays = rows.Sum(x => x.LeaveDays),
            HolidayDays = rows.Sum(x => x.HolidayDays),
            BlockedTasks = blocked,
            HoursAttainment = Rate(actualHours, plannedHours),
            UtilizationPercent = Rate(actualHours, availableDays * ExpectedHoursPerDay),
            UpdateCompliance = Rate(rows.Sum(x => x.DaysSubmitted), availableDays),
            BlockedPercent = Rate(blocked, planned),
            ExpectedHoursPerDay = ExpectedHoursPerDay,
            Rows = rows,
            StatusDistribution = TaskStatuses.All.Select(status => new StatusCountDto
            {
                Status = status,
                Count = allTasks.Count(x => x.Status == status)
            }).Where(x => x.Count > 0).ToList()
        };
    }

    public static string MonthName(int year, int month)
        => new DateOnly(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    private const decimal ExpectedHoursPerDay = 8m;
    private const int LowHoursMinutes = 360;

    private static List<DateOnly> ElapsedWeekdays(int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        var last = start.AddMonths(1).AddDays(-1);
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (start > today) return [];
        if (today < last) last = today;
        var days = new List<DateOnly>();
        for (var date = start; date <= last; date = date.AddDays(1))
            if (!WorkMetrics.IsWeekend(date)) days.Add(date);
        return days;
    }

    private readonly record struct DayScore(int Available, int Logged, int Submitted, int Missing, int Low, int Leave, int Holiday);

    private static DayScore ScoreDays(IReadOnlyList<DateOnly> weekdays, IReadOnlyDictionary<DateOnly, int>? minutes, IReadOnlyDictionary<DateOnly, DayMark>? marks)
    {
        var available = 0;
        var logged = 0;
        var submitted = 0;
        var missing = 0;
        var low = 0;
        var leave = 0;
        var holiday = 0;
        foreach (var date in weekdays)
        {
            DayMark? mark = null;
            if (marks is not null && marks.TryGetValue(date, out var found)) mark = found;
            if (mark?.DayType == DayTypes.Holiday) { holiday++; continue; }
            if (mark?.DayType == DayTypes.Leave) { leave++; continue; }
            available++;
            var worked = minutes is not null && minutes.TryGetValue(date, out var value) ? value : 0;
            if (worked == 0) missing++;
            else
            {
                logged++;
                if (worked < LowHoursMinutes) low++;
            }
            if (mark?.Status == SubmissionStatuses.Submitted) submitted++;
        }
        return new DayScore(available, logged, submitted, missing, low, leave, holiday);
    }

    private sealed record DayMark(string Status, string? DayType);

    private static decimal Rate(decimal part, decimal whole)
        => whole == 0 ? 0 : Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero);

    private static decimal Rate(int part, int whole) => Rate(part, (decimal)whole);
}
