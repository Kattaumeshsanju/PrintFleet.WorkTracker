using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IDashboardService
{
    Task<MyDashboardDto> MyAsync(CancellationToken ct);
    Task<TeamDashboardDto> TeamAsync(DateOnly? date, CancellationToken ct);
}

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IDailyWorkService _daily;
    private readonly IMonthlyWorkService _monthly;
    private readonly INotificationService _notifications;

    public DashboardService(AppDbContext db, ICurrentUser current, IDailyWorkService daily, IMonthlyWorkService monthly, INotificationService notifications)
    {
        _db = db;
        _current = current;
        _daily = daily;
        _monthly = monthly;
        _notifications = notifications;
    }

    public async Task<MyDashboardDto> MyAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var day = await _daily.GetDayAsync(today, _current.UserId, ct);
        var sheet = await _monthly.FindSheetAsync(_current.UserId, today.Year, today.Month, ct);
        var upcoming = (sheet?.Tasks ?? [])
            .Where(x => x.Status is TaskStatuses.Planned or TaskStatuses.InProgress or TaskStatuses.Blocked)
            .OrderBy(x => x.PlannedEndDate)
            .Take(6)
            .ToList();

        return new MyDashboardDto
        {
            Date = today,
            TodayMinutes = day.TotalMinutes,
            TasksCompleted = sheet?.Completed ?? 0,
            TasksInProgress = sheet?.InProgress ?? 0,
            DailyUpdateStatus = day.SubmissionStatus,
            MonthLocked = sheet?.Status == SheetStatuses.Locked,
            Today = day,
            Month = ToProgress(sheet),
            UpcomingTasks = upcoming,
            Notifications = await _notifications.GetAsync(ct)
        };
    }

    public async Task<TeamDashboardDto> TeamAsync(DateOnly? date, CancellationToken ct)
    {
        _current.EnsureLead();
        var day = date ?? DateOnly.FromDateTime(DateTime.Today);
        var users = await _db.Users.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(ct);
        var entries = await _db.DailyWorkEntries.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.MonthlyTask).ThenInclude(x => x.Project)
            .Where(x => x.WorkDate == day)
            .OrderBy(x => x.StartTime)
            .ToListAsync(ct);
        var submissions = await _db.DailySubmissions.AsNoTracking()
            .Where(x => x.WorkDate == day)
            .ToDictionaryAsync(x => x.UserId, x => x.Status, ct);

        var counts = await _db.MonthlyTasks.AsNoTracking()
            .Where(x => x.Sheet.Year == day.Year && x.Sheet.Month == day.Month && x.Sheet.User.IsActive)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var now = TimeOnly.FromDateTime(DateTime.Now);
        var members = users.Select(user =>
        {
            var mine = entries.Where(x => x.UserId == user.Id).OrderBy(x => x.StartTime).ToList();
            var current = mine.FirstOrDefault(x => x.StartTime <= now && x.EndTime > now) ?? mine.LastOrDefault();
            return new TeamMemberDayDto
            {
                UserId = user.Id,
                Name = user.Name,
                JobTitle = user.JobTitle,
                Role = user.Role,
                UpdateStatus = submissions.GetValueOrDefault(user.Id) ?? SubmissionStatuses.Pending,
                TotalMinutes = mine.Sum(x => x.DurationMinutes),
                CurrentActivity = current?.Description,
                CurrentTask = current?.MonthlyTask.Title,
                CurrentStart = current is null ? null : WorkMetrics.FormatTime(current.StartTime),
                CurrentEnd = current is null ? null : WorkMetrics.FormatTime(current.EndTime),
                CurrentStatus = current?.Status,
                Entries = mine.Select(DailyWorkService.MapEntry).ToList()
            };
        }).ToList();

        int Count(string status) => counts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;
        return new TeamDashboardDto
        {
            Date = day,
            MemberCount = users.Count,
            UpdatedToday = members.Count(x => x.UpdateStatus == SubmissionStatuses.Submitted),
            TotalMinutesToday = members.Sum(x => x.TotalMinutes),
            OpenTasks = Count(TaskStatuses.Planned) + Count(TaskStatuses.InProgress),
            CompletedThisMonth = Count(TaskStatuses.Completed),
            BlockedTasks = Count(TaskStatuses.Blocked),
            Members = members,
            Notifications = await _notifications.GetAsync(ct)
        };
    }

    private static MonthProgressDto ToProgress(MonthlySheetDto? sheet)
    {
        if (sheet is null) return new MonthProgressDto();
        return new MonthProgressDto
        {
            PlannedTasks = sheet.PlannedTasks,
            Completed = sheet.Completed,
            InProgress = sheet.InProgress,
            Blocked = sheet.Blocked,
            Cancelled = sheet.Cancelled,
            Planned = sheet.Planned,
            PlannedHours = sheet.PlannedHours,
            ActualHours = sheet.ActualHours,
            CompletionPercent = sheet.CompletionPercent
        };
    }
}
