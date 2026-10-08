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
    Task<TeamReportDto> TeamAsync(int year, int month, bool includeSelf, CancellationToken ct);
}

public class ReportService : IReportService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IMonthlyWorkService _monthly;
    private readonly IDailyWorkService _daily;

    public ReportService(AppDbContext db, ICurrentUser current, IMonthlyWorkService monthly, IDailyWorkService daily)
    {
        _db = db;
        _current = current;
        _monthly = monthly;
        _daily = daily;
    }

    public async Task<MonthlyReportDto> MonthlyAsync(int year, int month, Guid? userId, CancellationToken ct)
    {
        if (month is < 1 or > 12) throw new AppException("Month must be between 1 and 12.");
        var target = userId is null || userId == Guid.Empty || userId == _current.UserId ? _current.UserId : userId.Value;
        if (target != _current.UserId) _current.EnsureLead();

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

    public async Task<TeamReportDto> TeamAsync(int year, int month, bool includeSelf, CancellationToken ct)
    {
        _current.EnsureLead();
        if (month is < 1 or > 12) throw new AppException("Month must be between 1 and 12.");

        var plans = await _monthly.TeamAsync(year, month, null, null, null, null, null, ct);
        var rowsSource = plans.Employees.Where(x => includeSelf || x.User.Id != _current.UserId).ToList();
        var rows = rowsSource.Select(x => new TeamReportRowDto
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
            CompletionPercent = x.CompletionPercent
        }).OrderBy(x => x.Name).ToList();

        var allTasks = rowsSource.SelectMany(x => x.Tasks).ToList();
        var completed = rows.Sum(x => x.Completed);
        var planned = rows.Sum(x => x.Planned);
        return new TeamReportDto
        {
            Year = year,
            Month = month,
            IncludesSelf = includeSelf,
            PlannedTasks = planned,
            CompletedTasks = completed,
            PlannedHours = rows.Sum(x => x.PlannedHours),
            ActualHours = rows.Sum(x => x.ActualHours),
            CompletionPercent = WorkMetrics.Completion(completed, planned),
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
}
