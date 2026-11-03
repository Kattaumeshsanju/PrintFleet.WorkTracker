using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;
using PrintFleet.WorkTracker.Api.Services;

namespace PrintFleet.WorkTracker.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/organization-access")]
public class OrganizationAccessController : ControllerBase
{
    private readonly IOrganizationAccessService _access;

    public OrganizationAccessController(IOrganizationAccessService access) => _access = access;

    [HttpGet]
    public async Task<ActionResult<OrganizationAccessDto>> Get(CancellationToken ct)
        => Ok(await _access.GetAsync(ct));

    [HttpPost("ceo")]
    public async Task<ActionResult<CeoInvitationDto>> Invite(CreateCeoRequest request, CancellationToken ct)
        => Ok(await _access.InviteCeoAsync(request, ct));

    [HttpPost("invitations/{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await _access.RevokeInvitationAsync(id, ct);
        return NoContent();
    }

    [HttpPut("users/{id:guid}/overview")]
    public async Task<ActionResult<OrganizationPersonDto>> Overview(Guid id, SetGrantedRequest request, CancellationToken ct)
        => Ok(await _access.SetPermissionAsync(id, OrganizationPermissions.Overview, request.Granted, ct));

    [HttpPut("users/{id:guid}/reports")]
    public async Task<ActionResult<OrganizationPersonDto>> Reports(Guid id, SetGrantedRequest request, CancellationToken ct)
        => Ok(await _access.SetPermissionAsync(id, OrganizationPermissions.Reports, request.Granted, ct));

    [HttpPost("users/{id:guid}/status")]
    public async Task<ActionResult<OrganizationPersonDto>> Status(Guid id, SetDepartmentStatusRequest request, CancellationToken ct)
        => Ok(await _access.SetCeoStatusAsync(id, request.IsActive, ct));
}

[ApiController]
[AllowAnonymous]
[Route("api/ceo-invitations")]
public class CeoInvitationsController : ControllerBase
{
    private readonly IOrganizationAccessService _access;

    public CeoInvitationsController(IOrganizationAccessService access) => _access = access;

    [HttpPost("accept")]
    public async Task<IActionResult> Accept(AcceptInvitationRequest request, CancellationToken ct)
    {
        await _access.AcceptAsync(request, ct);
        return Ok(new { message = "Your password is set. Sign in with your work email." });
    }
}
