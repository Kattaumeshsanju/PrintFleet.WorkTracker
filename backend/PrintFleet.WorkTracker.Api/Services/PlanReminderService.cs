using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public class PlanReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IGraphMailbox _graph;
    private readonly IOptions<EmailOptions> _email;
    private readonly ILogger<PlanReminderService> _logger;
    private DateOnly? _notReadyFor;

    public PlanReminderService(IServiceScopeFactory scopes, IGraphMailbox graph, IOptions<EmailOptions> email, ILogger<PlanReminderService> logger)
    {
        _scopes = scopes;
        _graph = graph;
        _email = email;
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
                _logger.LogError(ex, "Monthly plan reminder check failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task SendIfDueAsync(CancellationToken ct)
    {
        var zone = PlanReminderRules.IndiaTime();
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await EnsureSettingsAsync(db, ct);
        if (!settings.Enabled || !PlanReminderRules.IsDue(local.DateTime, settings.Hour, settings.Minute, settings.Weekdays))
            return;
        if (!_graph.IsConfigured)
        {
            var today = DateOnly.FromDateTime(local.Date);
            if (_notReadyFor != today)
            {
                _notReadyFor = today;
                _logger.LogWarning("Monthly plan reminder is due, but Outlook Graph is not ready ({Readiness}). No plan emails were sent.", _graph.Readiness);
            }
            return;
        }

        var scheduled = DateOnly.FromDateTime(local.Date);
        var run = await db.PlanReminderRuns.FirstOrDefaultAsync(item => item.ScheduledDate == scheduled, ct);
        if (run?.Status == PlanReminderRunStatuses.Running && run.StartedAt > DateTime.UtcNow.AddMinutes(-10))
            return;

        var catalog = scope.ServiceProvider.GetRequiredService<PlanReminderCatalog>();
        var messages = await catalog.ComposeAsync(scheduled, settings.MissingPlanEnabled, settings.DepartmentSummaryEnabled, _email.Value.SiteUrl, ct);
        var pending = new List<PlanOutgoing>();
        foreach (var message in messages)
        {
            var key = PlanReminderRules.Key(scheduled, message.MessageType, message.UserId, scheduled.Year, scheduled.Month);
            var existing = await db.PlanReminderDeliveries.AsNoTracking().FirstOrDefaultAsync(item => item.IdempotencyKey == key, ct);
            if (existing is null || PlanReminderRules.MayAttempt(existing.Status, existing.Retryable, existing.Attempts, existing.NextAttemptAt, DateTime.UtcNow))
                pending.Add(message);
        }
        if (run?.Status == PlanReminderRunStatuses.Completed && pending.Count == 0)
            return;

        var now = DateTime.UtcNow;
        if (run is null)
        {
            run = new PlanReminderRun { Id = Guid.NewGuid(), ScheduledDate = scheduled, StartedAt = now, Status = PlanReminderRunStatuses.Running };
            db.PlanReminderRuns.Add(run);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.Entry(run).State = EntityState.Detached;
                return;
            }
        }
        else
        {
            run.Status = PlanReminderRunStatuses.Running;
            run.StartedAt = now;
            await db.SaveChangesAsync(ct);
        }

        var accepted = 0;
        var failed = 0;
        foreach (var message in pending)
        {
            var outcome = await SendOneAsync(db, run.Id, scheduled, message, ct);
            if (outcome == PlanReminderStatuses.Accepted) accepted++;
            else if (outcome == PlanReminderStatuses.Failed) failed++;
        }

        run.AcceptedCount = await db.PlanReminderDeliveries.CountAsync(item => item.RunId == run.Id && item.Status == PlanReminderStatuses.Accepted, ct);
        run.FailedCount = await db.PlanReminderDeliveries.CountAsync(item => item.RunId == run.Id && item.Status == PlanReminderStatuses.Failed, ct);
        run.FinishedAt = DateTime.UtcNow;
        run.Status = PlanReminderRunStatuses.Completed;
        run.FailureDetail = null;
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Monthly plan reminder for {Date}: accepted {Accepted}, failed {Failed}", scheduled, accepted, failed);
    }

    private static async Task<PlanReminderSettings> EnsureSettingsAsync(AppDbContext db, CancellationToken ct)
    {
        var settings = await db.PlanReminderSettings.FirstOrDefaultAsync(item => item.Id == PlanReminderRules.SettingsId, ct);
        if (settings is not null) return settings;
        settings = new PlanReminderSettings
        {
            Id = PlanReminderRules.SettingsId,
            Enabled = false,
            MissingPlanEnabled = true,
            DepartmentSummaryEnabled = false,
            Hour = 17,
            Minute = 0,
            Weekdays = "1,2,3,4,5",
            UpdatedAt = DateTime.UtcNow
        };
        db.PlanReminderSettings.Add(settings);
        await db.SaveChangesAsync(ct);
        return settings;
    }

    private async Task<string?> SendOneAsync(AppDbContext db, Guid runId, DateOnly scheduled, PlanOutgoing message, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var key = PlanReminderRules.Key(scheduled, message.MessageType, message.UserId, scheduled.Year, scheduled.Month);
        var row = await db.PlanReminderDeliveries.FirstOrDefaultAsync(item => item.IdempotencyKey == key, ct);
        if (row is null)
        {
            row = new PlanReminderDelivery
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                ScheduledDate = scheduled,
                DepartmentId = message.DepartmentId,
                DepartmentName = message.DepartmentName,
                UserId = message.UserId,
                RecipientEmail = message.Email,
                Year = scheduled.Year,
                Month = scheduled.Month,
                Subject = message.Message.Subject,
                MessageType = message.MessageType,
                Status = PlanReminderStatuses.Pending,
                IdempotencyKey = key,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.PlanReminderDeliveries.Add(row);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.Entry(row).State = EntityState.Detached;
                return null;
            }
        }

        if (!PlanReminderRules.MayAttempt(row.Status, row.Retryable, row.Attempts, row.NextAttemptAt, now))
            return null;

        var attempts = row.Attempts;
        var claimed = await db.PlanReminderDeliveries
            .Where(item => item.Id == row.Id && item.Status == row.Status && item.Attempts == attempts)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, PlanReminderStatuses.Processing)
                .SetProperty(item => item.Attempts, attempts + 1)
                .SetProperty(item => item.RunId, runId)
                .SetProperty(item => item.LastAttemptAt, now)
                .SetProperty(item => item.UpdatedAt, now)
                .SetProperty(item => item.Subject, message.Message.Subject)
                .SetProperty(item => item.RecipientEmail, message.Email), ct);
        if (claimed != 1) return null;

        GraphSendResult result;
        try
        {
            result = await _graph.SendAsync(message.Email, message.Message.Subject, message.Message.Text, message.Message.Html, null, null, ct);
        }
        catch (Exception ex)
        {
            result = new GraphSendResult(false, true, 0, ex.GetType().Name);
            _logger.LogWarning("Plan reminder send failed before Graph accepted it. {ErrorType}", ex.GetType().Name);
        }

        var retryable = !result.Accepted && result.Transient && attempts + 1 < PlanReminderRules.MaxAttempts;
        var status = result.Accepted ? PlanReminderStatuses.Accepted : PlanReminderStatuses.Failed;
        await db.PlanReminderDeliveries.Where(item => item.Id == row.Id).ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.Status, status)
            .SetProperty(item => item.Retryable, retryable)
            .SetProperty(item => item.FailureDetail, result.Accepted ? null : Trim(result.ErrorType))
            .SetProperty(item => item.NextAttemptAt, retryable ? now.AddMinutes(5) : null)
            .SetProperty(item => item.UpdatedAt, DateTime.UtcNow), ct);
        db.Entry(row).State = EntityState.Detached;
        return status;
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? "GraphRejected" : value.Length <= 200 ? value : value[..200];
}
