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
    private readonly IEmailSender _mail;
    private readonly IOptions<EmailOptions> _email;
    private readonly PlanReminderCatalog _catalog;

    public PlanReminderAdminService(AppDbContext db, ICurrentUser current, IGraphMailbox graph, IEmailSender mail, IOptions<EmailOptions> email, PlanReminderCatalog catalog)
    {
        _db = db;
        _current = current;
        _graph = graph;
        _mail = mail;
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
        var departments = await DepartmentChoicesAsync(ct);
        return Map(settings, messages.Count, probe, runs, departments);
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
        var choices = request.Departments ?? [];
        var ids = choices.Select(item => item.Id).ToHashSet();
        var departments = await _db.Departments.Where(item => ids.Contains(item.Id)).ToListAsync(ct);
        foreach (var department in departments)
            department.DailyReminderEnabled = choices.First(item => item.Id == department.Id).Enabled;
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
        var transport = PlanReminderRules.MailTransport(_graph.IsConfigured, _mail.CanSend && !_graph.IsConfigured);
        if (transport is null)
            throw new AppException($"Mail is not ready ({_graph.Readiness}). No test email was sent.");
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
            Transport = transport,
            Status = PlanReminderStatuses.Processing,
            Attempts = 1,
            LastAttemptAt = now,
            IdempotencyKey = $"test|{Guid.NewGuid():N}",
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.PlanReminderDeliveries.Add(row);
        await _db.SaveChangesAsync(ct);
        GraphSendResult result;
        if (transport == "Graph")
            result = await _graph.SendAsync(admin.Email.Trim(), preview.Subject, preview.Text, preview.Html, null, null, ct);
        else
        {
            try
            {
                await _mail.SendRichAsync(admin.Email.Trim(), preview.Subject, preview.Text, preview.Html, null, null, ct);
                result = new GraphSendResult(true, false, 250, null);
            }
            catch (Exception ex)
            {
                var graph = ex as GraphMailException;
                result = new GraphSendResult(false, graph?.Transient ?? true, graph?.StatusCode ?? 0, graph?.ErrorType ?? ex.GetType().Name);
            }
        }
        row.Status = result.Accepted ? PlanReminderStatuses.Accepted : PlanReminderStatuses.Failed;
        row.Retryable = !result.Accepted && result.Transient;
        row.FailureDetail = result.Accepted ? null : result.ErrorType;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        if (!result.Accepted)
            throw new AppException($"The mail server did not accept the test email ({row.FailureDetail}).");
        return MapDelivery(row);
    }

    public async Task<PagedResult<PlanReminderDeliveryDto>> HistoryAsync(int page, int pageSize, CancellationToken ct)
    {
        EnsureAdmin();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var plan = await _db.PlanReminderDeliveries.AsNoTracking().ToListAsync(ct);
        var daily = await (
            from item in _db.EmailDeliveries.AsNoTracking()
            where item.Kind == EmailDeliveryKinds.DailyReminder && item.UserId != null
            join user in _db.Users.AsNoTracking() on item.UserId equals user.Id
            join department in _db.Departments.AsNoTracking() on user.DepartmentId equals department.Id into departments
            from department in departments.DefaultIfEmpty()
            select new PlanReminderDeliveryDto
            {
                Id = item.Id,
                ScheduledDate = item.WorkDate ?? DateOnly.FromDateTime(item.UpdatedAt),
                DepartmentName = department != null ? department.Name : "",
                RecipientEmail = item.RecipientEmail ?? user.Email,
                Subject = item.Subject ?? "",
                MessageType = "Daily reminder",
                Attempts = item.Attempts,
                Status = item.Status,
                StatusLabel = item.Status == EmailDeliveryStatuses.SentToProvider ? "Accepted by mail server" : item.Status,
                LastAttemptAt = item.UpdatedAt,
                FailureDetail = item.ErrorType
            }).ToListAsync(ct);
        var combined = plan.Select(MapDelivery).Concat(daily).OrderByDescending(item => item.LastAttemptAt ?? DateTime.MinValue).ToList();
        var total = combined.Count;
        var items = combined.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<PlanReminderDeliveryDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
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

    private async Task<List<ReminderDepartmentDto>> DepartmentChoicesAsync(CancellationToken ct)
    {
        return await _db.Departments.AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new ReminderDepartmentDto
            {
                Id = item.Id,
                Name = item.Name,
                Enabled = item.DailyReminderEnabled,
                ActiveMembers = item.Users.Count(user => user.IsActive && (user.Role == Roles.TeamLead || user.Role == Roles.TeamMember))
            })
            .ToListAsync(ct);
    }

    private PlanReminderSettingsDto Map(PlanReminderSettings settings, int recipients, GraphSendResult probe, IReadOnlyList<PlanReminderRun> runs, IReadOnlyList<ReminderDepartmentDto> departments)
    {
        PlanReminderRun? lastSuccess = runs.FirstOrDefault(run => run.Status == PlanReminderRunStatuses.Completed && run.FailedCount == 0 && run.FinishedAt != null);
        PlanReminderRun? lastFailure = runs.FirstOrDefault(run => run.Status == PlanReminderRunStatuses.Failed || run.FailedCount > 0);
        var transport = PlanReminderRules.MailTransport(_graph.IsConfigured, _mail.CanSend && !_graph.IsConfigured) ?? "None";
        return new PlanReminderSettingsDto
        {
            OutlookReady = _graph.IsConfigured && probe.Accepted,
            OutlookStatus = _graph.IsConfigured ? (probe.Accepted ? "TokenReady" : probe.ErrorType ?? "TokenFailed") : _graph.Readiness,
            MailTransport = transport,
            Sender = _email.Value.FromAddress,
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
            LastFailureDetail = lastFailure?.FailureDetail,
            Departments = departments
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
        StatusLabel = row.Status == PlanReminderStatuses.Accepted ? PlanReminderRules.AcceptanceLabel(row.Transport) : row.Status,
        LastAttemptAt = row.LastAttemptAt,
        FailureDetail = row.FailureDetail
    };
}
