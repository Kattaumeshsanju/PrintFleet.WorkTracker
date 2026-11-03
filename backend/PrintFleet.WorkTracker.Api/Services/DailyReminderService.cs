using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public class DailyReminderService : BackgroundService
{
    private static readonly TimeZoneInfo India = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
    private readonly IServiceScopeFactory _scopes;
    private readonly IEmailSender _email;
    private readonly IOptions<EmailOptions> _options;
    private readonly ILogger<DailyReminderService> _logger;
    private DateOnly? _configWarningFor;

    public DailyReminderService(IServiceScopeFactory scopes, IEmailSender email, IOptions<EmailOptions> options, ILogger<DailyReminderService> logger)
    {
        _scopes = scopes;
        _email = email;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendIfDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Daily reminder check failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task SendIfDueAsync(CancellationToken ct)
    {
        var options = _options.Value;
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, India);
        var today = DateOnly.FromDateTime(local.Date);
        if (!options.Enabled || WorkMetrics.IsWeekend(today)) return;
        if (local.TimeOfDay < new TimeSpan(options.Hour, options.Minute, 0)) return;

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var marker = today.ToString("yyyy-MM-dd");
        var prior = await db.AuditLogs
            .Where(x => x.Action == "Reminder" && x.EntityType == "DailyReminder" && x.Details != null && x.Details.StartsWith(marker))
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
        if (!_email.CanSend)
        {
            if (_configWarningFor != today)
            {
                _configWarningFor = today;
                _logger.LogWarning("Daily reminder is due, but mail is not configured. Set Outlook Graph or Email host, username, password, and FromAddress.");
            }
            return;
        }

        var users = await db.Users.AsNoTracking()
            .Where(x => x.DepartmentId != null)
            .Select(x => new { x.Id, x.Email, x.Name, x.Role, x.IsActive, DepartmentActive = x.Department!.IsActive, x.Department!.DailyReminderEnabled })
            .ToListAsync(ct);
        var submissions = await db.DailySubmissions.AsNoTracking()
            .Where(x => x.WorkDate == today)
            .Select(x => new { x.UserId, x.Status, x.DayType })
            .ToListAsync(ct);
        var submissionByUser = submissions.ToDictionary(x => x.UserId);
        var selected = DailyReminderRules.Select(
            users.Select(x => new DailyReminderCandidate(x.Id, x.Email, x.Role, x.IsActive, x.DepartmentActive, x.DailyReminderEnabled)),
            id => submissionByUser.TryGetValue(id, out var row) ? row.Status : null,
            id => submissionByUser.TryGetValue(id, out var row) ? row.DayType : null);
        if (selected.Count == 0) return;
        var selectedIds = selected.Select(x => x.UserId).ToHashSet();
        var names = users.ToDictionary(x => x.Id, x => x.Name);
        var userIds = selectedIds.ToArray();
        var taskRows = await db.MonthlyTasks.AsNoTracking()
            .Where(x => x.Sheet.Year == today.Year && x.Sheet.Month == today.Month && userIds.Contains(x.Sheet.UserId)
                && (x.Status == TaskStatuses.Planned || x.Status == TaskStatuses.InProgress || x.Status == TaskStatuses.Blocked))
            .Select(x => new
            {
                UserId = x.Sheet.UserId,
                x.Title,
                Project = x.Project != null ? x.Project.Name : null,
                x.Status,
                x.Priority,
                x.PlannedStartDate,
                x.PlannedEndDate
            })
            .ToListAsync(ct);
        var tasksByUser = taskRows
            .Select(x => new TaskLine(x.UserId, x.Title, x.Project, x.Status, x.Priority, x.PlannedStartDate, x.PlannedEndDate))
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Start).ThenBy(x => x.Title).ToList());

        var sent = 0;
        var failed = 0;
        var site = options.SiteUrl.TrimEnd('/');
        var dailyLink = string.IsNullOrWhiteSpace(site) ? "" : $"{site}/daily";
        var planLink = string.IsNullOrWhiteSpace(site) ? "" : $"{site}/monthly-plan";
        var existing = await db.EmailDeliveries
            .Where(x => x.Kind == EmailDeliveryKinds.DailyReminder && x.WorkDate == today && x.UserId != null && selectedIds.Contains(x.UserId.Value))
            .ToListAsync(ct);
        var deliveryByUser = existing.ToDictionary(x => x.UserId!.Value);
        var pendingPeople = selected.Where(person =>
        {
            if (!deliveryByUser.TryGetValue(person.UserId, out var row)) return true;
            if (row.Status == EmailDeliveryStatuses.SentToProvider) return false;
            return row.Status == EmailDeliveryStatuses.Pending || TeamNoticeRules.CanRetry(row.Status, row.Attempts, row.ErrorType);
        }).ToList();
        if (pendingPeople.Count == 0) return;

        foreach (var person in pendingPeople)
        {
            deliveryByUser.TryGetValue(person.UserId, out var delivery);
            if (delivery?.Status == EmailDeliveryStatuses.SentToProvider) continue;
            if (delivery is not null && delivery.Status != EmailDeliveryStatuses.Pending && !TeamNoticeRules.CanRetry(delivery.Status, delivery.Attempts, delivery.ErrorType))
                continue;

            names.TryGetValue(person.UserId, out var name);
            tasksByUser.TryGetValue(person.UserId, out var ownTasks);
            var subject = $"Your tasks for {today:dd MMM yyyy}";
            var body = BuildBody(name ?? person.Email, today, true, false, ownTasks ?? [], dailyLink, planLink);
            var now = DateTime.UtcNow;
            if (delivery is null)
            {
                delivery = new EmailDelivery
                {
                    Id = Guid.NewGuid(),
                    Kind = EmailDeliveryKinds.DailyReminder,
                    UserId = person.UserId,
                    RecipientEmail = person.Email.Trim(),
                    Subject = subject,
                    WorkDate = today,
                    Status = EmailDeliveryStatuses.Pending,
                    Attempts = 0,
                    UpdatedAt = now
                };
                db.EmailDeliveries.Add(delivery);
                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    db.Entry(delivery).State = EntityState.Detached;
                    continue;
                }
            }

            delivery.Subject = subject;
            delivery.RecipientEmail = person.Email.Trim();
            delivery.UpdatedAt = now;
            var accepted = false;
            string? errorType = null;
            var permanent = false;
            try
            {
                await _email.SendAsync(person.Email.Trim(), subject, body, ct);
                accepted = true;
                sent++;
            }
            catch (Exception ex)
            {
                failed++;
                var graph = ex as GraphMailException;
                permanent = graph is not null && !graph.Transient;
                errorType = graph?.ErrorType ?? ex.GetType().Name;
                _logger.LogError("Could not send the daily reminder to {Address}. {ErrorType}", person.Email, errorType);
            }

            TeamNoticeRules.RecordAttempt(delivery, accepted, errorType, permanent, DateTime.UtcNow);
            await db.SaveChangesAsync(ct);
        }

        if (prior is null && (sent > 0 || failed > 0))
        {
            db.AuditLogs.Add(new AuditLog
            {
                Action = "Reminder",
                EntityType = "DailyReminder",
                UserName = "WorkTracker",
                Details = ReminderLedger.Describe(today, sent, users.Count - selected.Count, []),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
        _logger.LogInformation("Daily reminder for {Date}: accepted {Sent}, failed {Failed}", marker, sent, failed);
    }

    private sealed record TaskLine(Guid UserId, string Title, string? Project, string Status, string Priority, DateOnly Start, DateOnly End);

    private static string BuildBody(string name, DateOnly today, bool knownAccount, bool submitted, IReadOnlyList<TaskLine> tasks, string dailyLink, string planLink)
    {
        var dueToday = tasks.Where(x => x.Start <= today && x.End >= today).ToList();
        var later = tasks.Where(x => x.Start > today || x.End < today).ToList();
        var lines = new List<string> { $"Hello {name},", "" };

        if (!knownAccount)
        {
            lines.Add("WorkTracker has no account with this email address, so your tasks could not be listed.");
            lines.Add("Ask the Team Lead to set your login email to this address, then add tasks on My Monthly Plan.");
        }
        else if (tasks.Count == 0)
        {
            lines.Add($"You have no open tasks for {today:MMMM yyyy}.");
            lines.Add(string.IsNullOrWhiteSpace(planLink) ? "Add them on My Monthly Plan." : $"Add them here: {planLink}");
        }
        else
        {
            lines.Add($"Your open tasks for {today:dd MMM yyyy}:");
            lines.Add("");
            lines.Add("Due today");
            lines.Add(dueToday.Count == 0 ? "None." : string.Join(Environment.NewLine, dueToday.Select(FormatTask)));
            if (later.Count > 0)
            {
                lines.Add("");
                lines.Add("Still open this month");
                lines.Add(string.Join(Environment.NewLine, later.Select(FormatTask)));
            }
        }

        lines.Add("");
        lines.Add("To assess your KPI, fill in every working day with no gaps. Add each activity on the daily timeline and submit the day. A missing day, or a day left unsubmitted, is counted against plan completion, utilization, and update compliance. If you are on leave or the office is closed, mark that date as Leave or Holiday instead of leaving it blank.");
        lines.Add("");
        if (submitted)
            lines.Add("Today's timeline is already submitted.");
        else
            lines.Add(string.IsNullOrWhiteSpace(dailyLink)
                ? "Please log today's work on My Daily Timeline before you finish."
                : $"Please log today's work before you finish: {dailyLink}");

        if (knownAccount && !string.IsNullOrWhiteSpace(planLink))
            lines.Add($"Monthly plan: {planLink}");

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatTask(TaskLine task)
    {
        var project = string.IsNullOrWhiteSpace(task.Project) ? "" : $" ({task.Project})";
        var status = task.Status switch
        {
            TaskStatuses.InProgress => "In progress",
            TaskStatuses.Blocked => "Blocked",
            _ => "Planned"
        };
        return $"- {task.Title}{project} — {status} — {task.Priority} — {task.Start:dd MMM} to {task.End:dd MMM}";
    }
}
