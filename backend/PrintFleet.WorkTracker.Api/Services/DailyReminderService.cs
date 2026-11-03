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
        var retryOnly = prior is null ? null : ReminderLedger.FailedAddresses(prior.Details);
        if (prior is not null && retryOnly!.Count == 0) return;

        if (!options.IsConfigured)
        {
            if (_configWarningFor != today)
            {
                _configWarningFor = today;
                _logger.LogWarning("Daily reminder is due, but email is not configured. Set Email:Host, Username, Password, and FromAddress.");
            }
            return;
        }

        var recipients = (retryOnly ?? options.Recipients)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var users = await db.Users.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.Email, x.Name })
            .ToListAsync(ct);
        var byEmail = users.ToDictionary(x => x.Email, x => x, StringComparer.OrdinalIgnoreCase);
        var away = await db.DailySubmissions.AsNoTracking()
            .Where(x => x.WorkDate == today && x.DayType != null)
            .Select(x => x.UserId)
            .ToListAsync(ct);
        var awayIds = away.ToHashSet();
        var submitted = await db.DailySubmissions.AsNoTracking()
            .Where(x => x.WorkDate == today && x.Status == SubmissionStatuses.Submitted)
            .Select(x => x.UserId)
            .ToListAsync(ct);
        var submittedIds = submitted.ToHashSet();
        var userIds = users.Select(x => x.Id).ToArray();
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

        var sent = new List<string>();
        var skipped = new List<string>();
        var failed = new List<string>();
        var site = options.SiteUrl.TrimEnd('/');
        var dailyLink = string.IsNullOrWhiteSpace(site) ? "" : $"{site}/daily";
        var planLink = string.IsNullOrWhiteSpace(site) ? "" : $"{site}/monthly-plan";

        foreach (var address in recipients)
        {
            byEmail.TryGetValue(address, out var user);
            if (user is not null && awayIds.Contains(user.Id))
            {
                skipped.Add(address);
                continue;
            }

            var name = user?.Name ?? address;
            tasksByUser.TryGetValue(user?.Id ?? Guid.Empty, out var ownTasks);
            var body = BuildBody(name, today, user is not null, submittedIds.Contains(user?.Id ?? Guid.Empty), ownTasks ?? [], dailyLink, planLink);

            try
            {
                await _email.SendAsync(address, $"Your tasks for {today:dd MMM yyyy}", body, ct);
                sent.Add(address);
            }
            catch (Exception ex)
            {
                failed.Add(address);
                _logger.LogError("Could not send the daily reminder to {Address}. {ErrorType}", address, ex.GetType().Name);
            }
        }

        if (sent.Count == 0 && failed.Count > 0 && prior is null) return;

        var details = ReminderLedger.Describe(today, sent.Count, skipped.Count, failed);
        if (prior is null)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Action = "Reminder",
                EntityType = "DailyReminder",
                UserName = "WorkTracker",
                Details = details,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            prior.Details = details;
        }
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Daily reminder for {Date}: sent {Sent}, skipped {Skipped}, failed {Failed}", marker, sent.Count, skipped.Count, failed.Count);
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
