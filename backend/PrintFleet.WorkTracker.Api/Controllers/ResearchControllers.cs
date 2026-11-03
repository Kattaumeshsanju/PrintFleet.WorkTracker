using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;
using PrintFleet.WorkTracker.Api.Services;

namespace PrintFleet.WorkTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/work-categories")]
public class WorkCategoriesController : ControllerBase
{
    private readonly IWorkCategoryService _categories;

    public WorkCategoriesController(IWorkCategoryService categories) => _categories = categories;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkCategoryDto>>> List([FromQuery] Guid? departmentId, CancellationToken ct)
        => Ok(await _categories.ListAsync(departmentId, ct));

    [HttpPost]
    public async Task<ActionResult<WorkCategoryDto>> Create(SaveWorkCategoryRequest request, CancellationToken ct)
        => Ok(await _categories.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkCategoryDto>> Update(Guid id, SaveWorkCategoryRequest request, CancellationToken ct)
        => Ok(await _categories.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await _categories.DeactivateAsync(id, ct);
        return NoContent();
    }

    [HttpPost("reorder")]
    public async Task<IActionResult> Reorder(ReorderWorkCategoriesRequest request, CancellationToken ct)
    {
        await _categories.ReorderAsync(request, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reassign")]
    public async Task<IActionResult> Reassign(Guid id, ReassignWorkCategoryRequest request, CancellationToken ct)
    {
        await _categories.ReassignAsync(id, request, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/initiatives")]
public class InitiativesController : ControllerBase
{
    private const string Excel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private readonly IInitiativeService _initiatives;
    private readonly IExportService _export;

    public InitiativesController(IInitiativeService initiatives, IExportService export)
    {
        _initiatives = initiatives;
        _export = export;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<InitiativeDto>>> List([FromQuery] string? search, [FromQuery] Guid? departmentId, [FromQuery] Guid? categoryId, [FromQuery] string? status, [FromQuery] string? priority, [FromQuery] Guid? ownerId, [FromQuery] string? stage, [FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? sort, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await _initiatives.ListAsync(search, departmentId, categoryId, status, priority, ownerId, stage, year, month, sort, page, pageSize, ct));

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string? search, [FromQuery] Guid? departmentId, [FromQuery] Guid? categoryId, [FromQuery] string? status, [FromQuery] string? priority, [FromQuery] Guid? ownerId, [FromQuery] string? stage, [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        var rows = await _initiatives.ExportRowsAsync(search, departmentId, categoryId, status, priority, ownerId, stage, year, month, ct);
        return File(_export.InitiativesExcel(rows), Excel, "initiatives.xlsx");
    }

    [Authorize(Roles = Roles.Organization)]
    [HttpGet("portfolio")]
    public async Task<ActionResult<ResearchPortfolioDto>> Portfolio([FromQuery] Guid? departmentId, [FromQuery] Guid? categoryId, [FromQuery] string? status, [FromQuery] string? priority, [FromQuery] Guid? ownerId, [FromQuery] string? stage, [FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => Ok(await _initiatives.PortfolioAsync(departmentId, categoryId, status, priority, ownerId, stage, year, month, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InitiativeDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _initiatives.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<InitiativeDto>> Create(SaveInitiativeRequest request, CancellationToken ct)
        => Ok(await _initiatives.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InitiativeDto>> Update(Guid id, SaveInitiativeRequest request, CancellationToken ct)
        => Ok(await _initiatives.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/milestones")]
    public async Task<ActionResult<MilestoneDto>> AddMilestone(Guid id, SaveMilestoneRequest request, CancellationToken ct)
        => Ok(await _initiatives.AddMilestoneAsync(id, request, ct));

    [HttpPut("{id:guid}/milestones/{milestoneId:guid}")]
    public async Task<ActionResult<MilestoneDto>> UpdateMilestone(Guid id, Guid milestoneId, SaveMilestoneRequest request, CancellationToken ct)
        => Ok(await _initiatives.UpdateMilestoneAsync(id, milestoneId, request, ct));

    [HttpPost("{id:guid}/records")]
    public async Task<ActionResult<ResearchRecordDto>> AddRecord(Guid id, SaveResearchRecordRequest request, CancellationToken ct)
        => Ok(await _initiatives.AddRecordAsync(id, request, ct));
}
