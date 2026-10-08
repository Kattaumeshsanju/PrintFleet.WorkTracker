using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetAsync(CancellationToken ct);
}

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public NotificationService(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<IReadOnlyList<NotificationDto>> GetAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var notes = new List<NotificationDto>();
        if (_current.IsTeamLead) await AddLeadNotes(notes, today, ct);
        else await AddMemberNotes(notes, today, _current.UserId, ct);
        return notes.Take(18).ToList();
    }

    private async Task AddMemberNotes(List<NotificationDto> notes, DateOnly today, Guid userId, CancellationToken ct)
    {
        if (!WorkMetrics.IsWeekend(today))
        {
            var submitted = await _db.DailySubmissions.AnyAsync(
                x => x.UserId == userId && x.WorkDate == today && x.Status == SubmissionStatuses.Submitted, ct);
            if (!submitted)
            {
                notes.Add(new NotificationDto
                {
                    Severity = "warning",
                    Title = "Daily update pending",
                    Message = "You have not submitted today's work yet.",
                    Href = "/daily"
                });
            }
        }

        var tasks = await OpenTasks(ct, userId);
        foreach (var task in tasks.Where(x => x.PlannedEndDate < today).Take(4))
        {
            notes.Add(new NotificationDto
            {
                Severity = "critical",
                Title = "Task overdue",
                Message = $"{task.Title} was planned to finish on {task.PlannedEndDate:dd MMM}.",
                Href = "/monthly-plan"
            });
        }

        var soon = today.AddDays(3);
        foreach (var task in tasks.Where(x => x.Status != TaskStatuses.Blocked && x.PlannedEndDate >= today && x.PlannedEndDate <= soon).Take(3))
        {
            notes.Add(new NotificationDto
            {
                Severity = "info",
                Title = "Deadline approaching",
                Message = $"{task.Title} is planned to finish on {task.PlannedEndDate:dd MMM}.",
                Href = "/monthly-plan"
            });
        }

        foreach (var task in tasks.Where(x => x.Status == TaskStatuses.Blocked).Take(3))
        {
            notes.Add(new NotificationDto
            {
                Severity = "warning",
                Title = "Task blocked",
                Message = string.IsNullOrWhiteSpace(task.Remarks) ? task.Title : $"{task.Title}: {task.Remarks}",
                Href = "/monthly-plan"
            });
        }
    }

    private async Task AddLeadNotes(List<NotificationDto> notes, DateOnly today, CancellationToken ct)
    {
        var users = await _db.Users.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(ct);
        if (!WorkMetrics.IsWeekend(today))
        {
            var submittedIds = await _db.DailySubmissions.AsNoTracking()
                .Where(x => x.WorkDate == today && x.Status == SubmissionStatuses.Submitted)
                .Select(x => x.UserId)
                .ToListAsync(ct);
            foreach (var user in users.Where(x => !submittedIds.Contains(x.Id)).Take(7))
            {
                notes.Add(new NotificationDto
                {
                    Severity = "warning",
                    Title = "Daily update pending",
                    Message = $"{user.Name} has not submitted today's work.",
                    Href = $"/daily?userId={user.Id}&date={today:yyyy-MM-dd}"
                });
            }
        }

        var previous = today.AddDays(-1);
        while (WorkMetrics.IsWeekend(previous)) previous = previous.AddDays(-1);
        var hours = await _db.DailyWorkEntries.AsNoTracking()
            .Where(x => x.WorkDate == previous)
            .GroupBy(x => x.UserId)
            .Select(g => new { g.Key, Minutes = g.Sum(x => x.DurationMinutes) })
            .ToDictionaryAsync(x => x.Key, x => x.Minutes, ct);

        foreach (var user in users)
        {
            var minutes = hours.GetValueOrDefault(user.Id);
            if (minutes == 0)
            {
                notes.Add(new NotificationDto
                {
                    Severity = "warning",
                    Title = "Missing work log",
                    Message = $"{user.Name} has no work logged for {previous:dd MMM}.",
                    Href = $"/team/{user.Id}"
                });
            }
            else if (minutes < 360)
            {
                notes.Add(new NotificationDto
                {
                    Severity = "info",
                    Title = "Low working hours",
                    Message = $"{user.Name} logged {WorkMetrics.FormatMinutes(minutes)} on {previous:dd MMM}.",
                    Href = $"/team/{user.Id}"
                });
            }
        }

        var monthTasks = await _db.MonthlyTasks.AsNoTracking()
            .Include(x => x.Sheet).ThenInclude(x => x.User)
            .Where(x => x.Sheet.Year == today.Year && x.Sheet.Month == today.Month && x.Sheet.User.IsActive)
            .ToListAsync(ct);

        foreach (var task in monthTasks.Where(x => TaskStatuses.Open.Contains(x.Status) && x.PlannedEndDate < today).Take(5))
        {
            notes.Add(new NotificationDto
            {
                Severity = "critical",
                Title = "Task overdue",
                Message = $"{task.Sheet.User.Name}: {task.Title} was due {task.PlannedEndDate:dd MMM}.",
                Href = $"/team/{task.Sheet.UserId}"
            });
        }

        foreach (var task in monthTasks.Where(x => x.Status == TaskStatuses.Blocked).Take(4))
        {
            notes.Add(new NotificationDto
            {
                Severity = "warning",
                Title = "Blocked task",
                Message = $"{task.Sheet.User.Name}: {task.Title}",
                Href = $"/team/{task.Sheet.UserId}"
            });
        }

        var withTasks = monthTasks.Select(x => x.Sheet.UserId).ToHashSet();
        foreach (var user in users.Where(x => !withTasks.Contains(x.Id)).Take(4))
        {
            notes.Add(new NotificationDto
            {
                Severity = "info",
                Title = "Monthly plan incomplete",
                Message = $"{user.Name} has no planned tasks for this month.",
                Href = $"/monthly-plan?userId={user.Id}"
            });
        }
    }

    private Task<List<MonthlyTask>> OpenTasks(CancellationToken ct, Guid userId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return _db.MonthlyTasks.AsNoTracking()
            .Where(x => x.Sheet.UserId == userId && x.Sheet.Year == today.Year && x.Sheet.Month == today.Month && TaskStatuses.Open.Contains(x.Status))
            .OrderBy(x => x.PlannedEndDate)
            .ToListAsync(ct);
    }
}
