using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IPlanReminderAdminService
{
    Task<PlanReminderSettingsDto> GetAsync(CancellationToken ct);
    Task<PlanReminderSettingsDto> SaveAsync(SavePlanReminderSettingsRequest request, CancellationToken ct);
    Task<PlanReminderPreviewDto> PreviewAsync(CancellationToken ct);
    Task<PlanReminderDeliveryDto> SendTestAsync(CancellationToken ct);
    Task<PagedResult<PlanReminderDeliveryDto>> HistoryAsync(int page, int pageSize, CancellationToken ct);
}

public class PlanReminderAdminService : IPlanReminderAdminService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IGraphMailbox _graph;
    private readonly IOptions<EmailOptions> _email;
    private readonly PlanReminderCatalog _catalog;

    public PlanReminderAdminService(AppDbContext db, ICurrentUser current, IGraphMailbox graph, IOptions<EmailOptions> email, PlanReminderCatalog catalog)
    {
        _db = db;
        _current = current;
        _graph = graph;
        _email = email;
        _catalog = catalog;
    }

    public async Task<PlanReminderSettingsDto> GetAsync(CancellationToken ct)
    {
        EnsureAdmin();
        var settings = await EnsureSettingsAsync(ct);
        var zone = PlanReminderRules.IndiaTime();
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        var today = DateOnly.FromDateTime(local.Date);
        var messages = await _catalog.ComposeAsync(today, settings.MissingPlanEnabled, settings.DepartmentSummaryEnabled, _email.Value.SiteUrl, ct);
        var probe = await _graph.ProbeAsync(ct);
        var runs = await _db.PlanReminderRuns.AsNoTracking().OrderByDescending(run => run.StartedAt).Take(20).ToListAsync(ct);
        return Map(settings, messages.Count, probe, runs);
    }

    public async Task<PlanReminderSettingsDto> SaveAsync(SavePlanReminderSettingsRequest request, CancellationToken ct)
    {
        EnsureAdmin();
        if (request.Hour is < 0 or > 23 || request.Minute is < 0 or > 59)
            throw new AppException("Choose an hour from 0 to 23 and a minute from 0 to 59.");
        var days = string.Join(",", request.Weekdays.Where(day => day is >= 0 and <= 6).Distinct().Order());
        if (days.Length == 0)
            throw new AppException("Choose at least one weekday.");
        var settings = await EnsureSettingsAsync(ct);
        settings.Enabled = request.Enabled;
        settings.MissingPlanEnabled = request.MissingPlanEnabled;
        settings.DepartmentSummaryEnabled = request.DepartmentSummaryEnabled;
        settings.Hour = request.Hour;
        settings.Minute = request.Minute;
        settings.Weekdays = days;
        settings.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(ct);
    }

    public async Task<PlanReminderPreviewDto> PreviewAsync(CancellationToken ct)
    {
        EnsureAdmin();
        var settings = await EnsureSettingsAsync(ct);
        var zone = PlanReminderRules.IndiaTime();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date);
        var messages = await _catalog.ComposeAsync(today, true, settings.DepartmentSummaryEnabled, _email.Value.SiteUrl, ct);
        var own = messages.FirstOrDefault(message => message.UserId == _current.UserId && message.MessageType != PlanReminderTypes.DepartmentSummary);
        if (own is not null)
            return new PlanReminderPreviewDto { Subject = own.Message.Subject, Text = own.Message.Text, Html = own.Message.Html, Sample = false };
        var sample = PlanReminderMail.Personal(
            "Preview employee",
            "Sample department",
            "Preview lead",
            new DateOnly(today.Year, today.Month, 1),
            [new PlanGoalLine("Sample goal <not real>", "Description", "Sample project", TaskStatuses.InProgress, 40, today, today.AddDays(5), null, 8)],
            string.IsNullOrWhiteSpace(_email.Value.SiteUrl) ? "" : $"{_email.Value.SiteUrl.TrimEnd('/')}/monthly-plan",
            today);
        return new PlanReminderPreviewDto { Subject = sample.Subject, Text = sample.Text, Html = sample.Html, Sample = true };
    }

    public async Task<PlanReminderDeliveryDto> SendTestAsync(CancellationToken ct)
    {
        EnsureAdmin();
        if (!_graph.IsConfigured)
            throw new AppException($"Outlook is not ready ({_graph.Readiness}). No test email was sent.");
        var admin = await _db.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == _current.UserId && user.IsActive, ct)
            ?? throw new AppException("Your account is not active.", StatusCodes.Status403Forbidden);
        if (!PlanReminderRules.IsValidEmail(admin.Email))
            throw new AppException("Your account does not have a valid email address.");
        var preview = await PreviewAsync(ct);
        var zone = PlanReminderRules.IndiaTime();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date);
        var now = DateTime.UtcNow;
        var row = new PlanReminderDelivery
        {
            Id = Guid.NewGuid(),
            ScheduledDate = today,
            DepartmentName = "Test",
            UserId = admin.Id,
            RecipientEmail = admin.Email.Trim(),
            Year = today.Year,
            Month = today.Month,
            Subject = preview.Subject,
            MessageType = PlanReminderTypes.Test,
            Status = PlanReminderStatuses.Processing,
            Attempts = 1,
            LastAttemptAt = now,
            IdempotencyKey = $"test|{Guid.NewGuid():N}",
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.PlanReminderDeliveries.Add(row);
        await _db.SaveChangesAsync(ct);
        var result = await _graph.SendAsync(admin.Email.Trim(), preview.Subject, preview.Text, preview.Html, null, null, ct);
        row.Status = result.Accepted ? PlanReminderStatuses.Accepted : PlanReminderStatuses.Failed;
        row.Retryable = !result.Accepted && result.Transient;
        row.FailureDetail = result.Accepted ? null : result.ErrorType;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        if (!result.Accepted)
            throw new AppException($"Microsoft Graph did not accept the test email ({row.FailureDetail}).");
        return MapDelivery(row);
    }

    public async Task<PagedResult<PlanReminderDeliveryDto>> HistoryAsync(int page, int pageSize, CancellationToken ct)
    {
        EnsureAdmin();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.PlanReminderDeliveries.AsNoTracking().OrderByDescending(item => item.UpdatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<PlanReminderDeliveryDto> { Items = items.Select(MapDelivery).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    private void EnsureAdmin()
    {
        if (!_current.IsAdmin)
            throw new AppException("Only an admin can change reminder settings.", StatusCodes.Status403Forbidden);
    }

    private async Task<PlanReminderSettings> EnsureSettingsAsync(CancellationToken ct)
    {
        var settings = await _db.PlanReminderSettings.FirstOrDefaultAsync(item => item.Id == PlanReminderRules.SettingsId, ct);
        if (settings is not null) return settings;
        settings = new PlanReminderSettings
        {
            Id = PlanReminderRules.SettingsId,
            MissingPlanEnabled = true,
            Hour = 17,
            Minute = 0,
            Weekdays = "1,2,3,4,5",
            UpdatedAt = DateTime.UtcNow
        };
        _db.PlanReminderSettings.Add(settings);
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    private PlanReminderSettingsDto Map(PlanReminderSettings settings, int recipients, GraphSendResult probe, IReadOnlyList<PlanReminderRun> runs)
    {
        PlanReminderRun? lastSuccess = runs.FirstOrDefault(run => run.Status == PlanReminderRunStatuses.Completed && run.FailedCount == 0 && run.FinishedAt != null);
        PlanReminderRun? lastFailure = runs.FirstOrDefault(run => run.Status == PlanReminderRunStatuses.Failed || run.FailedCount > 0);
        return new PlanReminderSettingsDto
        {
            OutlookReady = _graph.IsConfigured && probe.Accepted,
            OutlookStatus = _graph.IsConfigured ? (probe.Accepted ? "TokenReady" : probe.ErrorType ?? "TokenFailed") : _graph.Readiness,
            Sender = _graph.Sender ?? "",
            Enabled = settings.Enabled,
            MissingPlanEnabled = settings.MissingPlanEnabled,
            DepartmentSummaryEnabled = settings.DepartmentSummaryEnabled,
            Hour = settings.Hour,
            Minute = settings.Minute,
            Weekdays = PlanReminderRules.ParseWeekdays(settings.Weekdays).Order().ToList(),
            TimeZone = "Asia/Kolkata",
            RecipientCount = recipients,
            LastSuccessAt = lastSuccess?.FinishedAt,
            LastFailureAt = lastFailure?.FinishedAt ?? lastFailure?.StartedAt,
            LastFailureDetail = lastFailure?.FailureDetail
        };
    }

    private static PlanReminderDeliveryDto MapDelivery(PlanReminderDelivery row) => new()
    {
        Id = row.Id,
        ScheduledDate = row.ScheduledDate,
        DepartmentName = row.DepartmentName,
        RecipientEmail = row.RecipientEmail,
        Year = row.Year,
        Month = row.Month,
        Subject = row.Subject,
        MessageType = row.MessageType,
        Attempts = row.Attempts,
        Status = row.Status,
        StatusLabel = row.Status == PlanReminderStatuses.Accepted ? "Accepted by Microsoft Graph" : row.Status,
        LastAttemptAt = row.LastAttemptAt,
        FailureDetail = row.FailureDetail
    };
}
