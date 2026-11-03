using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;
using PrintFleet.WorkTracker.Api.Services;

namespace PrintFleet.WorkTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/my-team")]
public class MyTeamController : ControllerBase
{
    private readonly ITeamLeadService _team;

    public MyTeamController(ITeamLeadService team) => _team = team;

    [HttpGet]
    public async Task<ActionResult<MyTeamDto>> Get(CancellationToken ct)
        => Ok(await _team.GetAsync(ct));

    [HttpGet("candidates")]
    public async Task<ActionResult<IReadOnlyList<TeamPersonDto>>> Search([FromQuery] string? search, CancellationToken ct)
        => Ok(await _team.SearchAsync(search, ct));

    [HttpPost("members")]
    public async Task<IActionResult> Add(AddTeamMemberRequest request, CancellationToken ct)
    {
        await _team.AddExistingAsync(request, ct);
        return NoContent();
    }

    [HttpDelete("members/{userId:guid}")]
    public async Task<IActionResult> Remove(Guid userId, CancellationToken ct)
    {
        await _team.RemoveAsync(userId, ct);
        return NoContent();
    }

    [HttpPost("invitations")]
    public async Task<ActionResult<TeamInviteDto>> Invite(InviteTeamMemberRequest request, CancellationToken ct)
        => Ok(await _team.InviteAsync(request, ct));

    [HttpPost("invitations/{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await _team.RevokeInviteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("notifications")]
    public async Task<ActionResult<TeamNoticeHistoryDto>> Send(SendTeamNoticeRequest request, CancellationToken ct)
        => Ok(await _team.SendAsync(request, ct));

    [HttpGet("notifications")]
    public async Task<ActionResult<IReadOnlyList<TeamNoticeHistoryDto>>> History(CancellationToken ct)
        => Ok(await _team.HistoryAsync(ct));

    [HttpPost("notifications/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var queued = await _team.RetryAsync(id, ct);
        return queued ? NoContent() : Conflict(new { message = "There is no failed delivery that can be retried." });
    }
}

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/team-notices")]
public class TeamNoticesController : ControllerBase
{
    private readonly ITeamLeadService _team;

    public TeamNoticesController(ITeamLeadService team) => _team = team;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TeamNoticeHistoryDto>>> List(CancellationToken ct)
        => Ok(await _team.OrganizationHistoryAsync(ct));
}

[ApiController]
[Route("api/team-invitations")]
public class TeamInvitationsController : ControllerBase
{
    private readonly ITeamLeadService _team;

    public TeamInvitationsController(ITeamLeadService team) => _team = team;

    [AllowAnonymous]
    [HttpPost("accept")]
    public async Task<IActionResult> Accept(AcceptInvitationRequest request, CancellationToken ct)
    {
        await _team.AcceptInviteAsync(request, ct);
        return Ok(new { message = "Your account is ready. Sign in with your work email." });
    }
}
