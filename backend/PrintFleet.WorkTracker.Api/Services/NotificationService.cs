using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetAsync(CancellationToken ct);
    Task MarkReadAsync(Guid id, CancellationToken ct);
    Task MarkAllReadAsync(CancellationToken ct);
}

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IDepartmentAccess _access;

    public NotificationService(AppDbContext db, ICurrentUser current, IDepartmentAccess access)
    {
        _db = db;
        _current = current;
        _access = access;
    }

    public async Task<IReadOnlyList<NotificationDto>> GetAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var notes = new List<NotificationDto>();
        notes.AddRange(await StoredAsync(ct));
        if (_current.Role == Roles.Ceo) await AddExecutiveNotes(notes, today, ct);
        else if (_current.IsTeamLead) await AddLeadNotes(notes, today, ct);
        else await AddMemberNotes(notes, today, _current.UserId, ct);
        return notes.Take(30).ToList();
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.TeamNoticeRecipients.FirstOrDefaultAsync(x => x.Id == id && x.UserId == _current.UserId, ct);
        if (row is null) return;
        row.ReadAt ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(CancellationToken ct)
    {
        var rows = await _db.TeamNoticeRecipients.Where(x => x.UserId == _current.UserId && x.ReadAt == null).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var row in rows) row.ReadAt = now;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<List<NotificationDto>> StoredAsync(CancellationToken ct)
    {
        var rows = await _db.TeamNoticeRecipients.AsNoTracking()
            .Where(x => x.UserId == _current.UserId && x.ReadAt == null && (x.Notice.Channel == NoticeChannels.InApp || x.Notice.Channel == NoticeChannels.Both))
            .OrderByDescending(x => x.Notice.CreatedAt)
            .Take(20)
            .Select(x => new { x.Id, x.Notice.Title, x.Notice.Body, x.Notice.LinkPath, Sender = x.Notice.Sender.Name, x.Notice.CreatedAt })
            .ToListAsync(ct);
        return rows.Select(x => new NotificationDto
        {
            Id = x.Id,
            Severity = "info",
            Title = x.Title,
            Message = x.Body.Length <= 240 ? x.Body : x.Body[..240],
            Href = x.LinkPath is "/projects" or "/plans" ? x.LinkPath : null,
            Unread = true,
            SenderName = x.Sender,
            CreatedAt = x.CreatedAt
        }).ToList();
    }

    private async Task AddExecutiveNotes(List<NotificationDto> notes, DateOnly today, CancellationToken ct)
    {
        var overdue = await _db.MonthlyTasks.CountAsync(
            x => x.Sheet.Year == today.Year && x.Sheet.Month == today.Month && x.PlannedEndDate < today && TaskStatuses.Open.Contains(x.Status), ct);
        var blocked = await _db.MonthlyTasks.CountAsync(
            x => x.Sheet.Year == today.Year && x.Sheet.Month == today.Month && x.Status == TaskStatuses.Blocked, ct);
        if (overdue > 0)
            notes.Add(new NotificationDto { Severity = "critical", Title = "Overdue goals", Message = $"{overdue} open goal{(overdue == 1 ? "" : "s")} are past their target date.", Href = "/executive/risks" });
        if (blocked > 0)
            notes.Add(new NotificationDto { Severity = "warning", Title = "Blocked goals", Message = $"{blocked} goal{(blocked == 1 ? "" : "s")} are blocked.", Href = "/executive/risks" });
    }

    private async Task AddMemberNotes(List<NotificationDto> notes, DateOnly today, Guid userId, CancellationToken ct)
    {
        if (!WorkMetrics.IsWeekend(today))
        {
            var covered = await _db.DailySubmissions.AnyAsync(
                x => x.UserId == userId && x.WorkDate == today && (x.Status == SubmissionStatuses.Submitted || x.DayType != null), ct);
            if (!covered)
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
        var usersQuery = await _access.ScopeUsersAsync(_db.Users.AsNoTracking().Where(x => x.IsActive), null, ct);
        var users = await usersQuery.OrderBy(x => x.Name).ToListAsync(ct);
        var userIds = users.Select(x => x.Id).ToArray();
        if (await _access.IsOrganizationViewerAsync(ct) && !WorkMetrics.IsWeekend(today))
        {
            var submittedIds = await _db.DailySubmissions.AsNoTracking()
                .Where(x => x.WorkDate == today && (x.Status == SubmissionStatuses.Submitted || x.DayType != null))
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
        if (await _access.IsOrganizationViewerAsync(ct))
        {
        var markedIds = await _db.DailySubmissions.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) && x.WorkDate == previous && x.DayType != null)
            .Select(x => x.UserId)
            .ToListAsync(ct);
        var marked = markedIds.ToHashSet();
        var hours = await _db.DailyWorkEntries.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) && x.WorkDate == previous)
            .GroupBy(x => x.UserId)
            .Select(g => new { g.Key, Minutes = g.Sum(x => x.DurationMinutes) })
            .ToDictionaryAsync(x => x.Key, x => x.Minutes, ct);

        foreach (var user in users)
        {
            if (marked.Contains(user.Id)) continue;
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
        }

        var monthTasks = await _db.MonthlyTasks.AsNoTracking()
            .Include(x => x.Sheet).ThenInclude(x => x.User)
            .Where(x => userIds.Contains(x.Sheet.UserId) && x.Sheet.Year == today.Year && x.Sheet.Month == today.Month && x.Sheet.User.IsActive)
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
