using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;
using PrintFleet.WorkTracker.Api.Services;

namespace PrintFleet.WorkTracker.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
        => Ok(await _auth.LoginAsync(request, ct));

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
        => Ok(await _auth.MeAsync(ct));

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await _auth.ChangePasswordAsync(request, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users) => _users = users;

    [Authorize(Roles = Roles.Readers)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await _users.ListAsync(includeInactive, ct));

    [Authorize(Roles = Roles.Managers)]
    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(SaveUserRequest request, CancellationToken ct)
    {
        var user = await _users.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Overview), new { id = user.Id }, user);
    }

    [Authorize(Roles = Roles.Managers)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserDto>> Update(Guid id, SaveUserRequest request, CancellationToken ct)
        => Ok(await _users.UpdateAsync(id, request, ct));

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("{id:guid}/organization-role")]
    public async Task<ActionResult<UserDto>> OrganizationRole(Guid id, SetOrganizationRoleRequest request, CancellationToken ct)
        => Ok(await _users.SetOrganizationRoleAsync(id, request.Role, request.DepartmentId, ct));

    [Authorize(Roles = Roles.Managers)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await _users.DeactivateAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/overview")]
    public async Task<ActionResult<EmployeeOverviewDto>> Overview(Guid id, [FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        var today = DateTime.Today;
        if (year == 0) year = today.Year;
        if (month == 0) month = today.Month;
        return Ok(await _users.OverviewAsync(id, year, month, ct));
    }
}

[ApiController]
[Authorize]
[Route("api/departments")]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departments;

    public DepartmentsController(IDepartmentService departments) => _departments = departments;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> List(CancellationToken ct)
        => Ok(await _departments.ListAsync(ct));

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<ActionResult<DepartmentDto>> Create(SaveDepartmentRequest request, CancellationToken ct)
    {
        var department = await _departments.CreateAsync(request, ct);
        return Created($"/api/departments/{department.Id}", department);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DepartmentDto>> Rename(Guid id, SaveDepartmentRequest request, CancellationToken ct)
        => Ok(await _departments.RenameAsync(id, request, ct));

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<DepartmentDto>> Status(Guid id, SetDepartmentStatusRequest request, CancellationToken ct)
        => Ok(await _departments.SetStatusAsync(id, request.IsActive, ct));

    [Authorize(Roles = Roles.Managers)]
    [HttpPut("{id:guid}/team")]
    public async Task<ActionResult<DepartmentDto>> SaveTeam(Guid id, SaveDepartmentTeamRequest request, CancellationToken ct)
        => Ok(await _departments.SaveTeamAsync(id, request, ct));

    [Authorize(Roles = Roles.Managers)]
    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<DepartmentDto>> Assign(Guid id, AssignDepartmentMemberRequest request, CancellationToken ct)
        => Ok(await _departments.AssignMemberAsync(id, request, ct));
}

[ApiController]
[Authorize]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projects;

    public ProjectsController(IProjectService projects) => _projects = projects;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> List([FromQuery] string? search, [FromQuery] string? status, [FromQuery] Guid? departmentId, CancellationToken ct)
        => Ok(await _projects.ListAsync(search, status, departmentId, ct));

    [Authorize(Roles = Roles.Managers)]
    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(SaveProjectRequest request, CancellationToken ct)
    {
        var project = await _projects.CreateAsync(request, ct);
        return Created($"/api/projects/{project.Id}", project);
    }

    [Authorize(Roles = Roles.Managers)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Update(Guid id, SaveProjectRequest request, CancellationToken ct)
        => Ok(await _projects.UpdateAsync(id, request, ct));

    [Authorize(Roles = Roles.Managers)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _projects.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _audit;

    public AuditController(IAuditService audit) => _audit = audit;

    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default)
        => Ok(await _audit.ListAsync(page, pageSize, ct));
}
