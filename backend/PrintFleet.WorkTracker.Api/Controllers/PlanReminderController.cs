using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;
using PrintFleet.WorkTracker.Api.Services;

namespace PrintFleet.WorkTracker.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/plan-reminders")]
public class PlanReminderController : ControllerBase
{
    private readonly IPlanReminderAdminService _reminders;

    public PlanReminderController(IPlanReminderAdminService reminders) => _reminders = reminders;

    [HttpGet("settings")]
    public async Task<ActionResult<PlanReminderSettingsDto>> Settings(CancellationToken ct)
        => Ok(await _reminders.GetAsync(ct));

    [HttpPut("settings")]
    public async Task<ActionResult<PlanReminderSettingsDto>> Save(SavePlanReminderSettingsRequest request, CancellationToken ct)
        => Ok(await _reminders.SaveAsync(request, ct));

    [HttpGet("preview")]
    public async Task<ActionResult<PlanReminderPreviewDto>> Preview(CancellationToken ct)
        => Ok(await _reminders.PreviewAsync(ct));

    [HttpPost("test")]
    public async Task<ActionResult<PlanReminderDeliveryDto>> Test(CancellationToken ct)
        => Ok(await _reminders.SendTestAsync(ct));

    [HttpGet("history")]
    public async Task<ActionResult<PagedResult<PlanReminderDeliveryDto>>> History([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await _reminders.HistoryAsync(page, pageSize, ct));
}
