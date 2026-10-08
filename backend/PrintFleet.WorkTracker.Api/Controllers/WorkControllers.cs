using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;
using PrintFleet.WorkTracker.Api.Services;

namespace PrintFleet.WorkTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/monthly-work")]
public class MonthlyWorkController : ControllerBase
{
    private readonly IMonthlyWorkService _monthly;

    public MonthlyWorkController(IMonthlyWorkService monthly) => _monthly = monthly;

    [HttpPost]
    public async Task<ActionResult<MonthlySheetDto>> Ensure(EnsureSheetRequest request, CancellationToken ct)
        => Ok(await _monthly.EnsureAsync(request, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MonthlySheetDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _monthly.GetAsync(id, ct));

    [HttpGet("team")]
    public async Task<ActionResult<TeamPlansDto>> Team([FromQuery] int year, [FromQuery] int month, [FromQuery] Guid? userId, [FromQuery] Guid? projectId, [FromQuery] string? status, [FromQuery] string? priority, [FromQuery] string? search, CancellationToken ct)
    {
        var today = DateTime.Today;
        if (year == 0) year = today.Year;
        if (month == 0) month = today.Month;
        return Ok(await _monthly.TeamAsync(year, month, userId, projectId, status, priority, search, ct));
    }

    [HttpPost("{id:guid}/tasks")]
    public async Task<ActionResult<MonthlyTaskDto>> AddTask(Guid id, SaveTaskRequest request, CancellationToken ct)
    {
        var task = await _monthly.AddTaskAsync(id, request, ct);
        return Created($"/api/monthly-tasks/{task.Id}", task);
    }

    [Authorize(Roles = Roles.TeamLead)]
    [HttpPost("{id:guid}/lock")]
    public async Task<ActionResult<MonthlySheetDto>> Lock(Guid id, CancellationToken ct)
        => Ok(await _monthly.SetLockAsync(id, true, ct));

    [Authorize(Roles = Roles.TeamLead)]
    [HttpPost("{id:guid}/unlock")]
    public async Task<ActionResult<MonthlySheetDto>> Unlock(Guid id, CancellationToken ct)
        => Ok(await _monthly.SetLockAsync(id, false, ct));
}

[ApiController]
[Authorize]
[Route("api/monthly-tasks")]
public class MonthlyTasksController : ControllerBase
{
    private readonly IMonthlyWorkService _monthly;

    public MonthlyTasksController(IMonthlyWorkService monthly) => _monthly = monthly;

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MonthlyTaskDto>> Update(Guid id, SaveTaskRequest request, CancellationToken ct)
        => Ok(await _monthly.UpdateTaskAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _monthly.DeleteTaskAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/daily-work")]
public class DailyWorkController : ControllerBase
{
    private readonly IDailyWorkService _daily;

    public DailyWorkController(IDailyWorkService daily) => _daily = daily;

    [HttpGet]
    public async Task<ActionResult<PagedResult<DailyEntryDto>>> List([FromQuery] DailyWorkQuery query, CancellationToken ct)
        => Ok(await _daily.ListAsync(query, ct));

    [HttpGet("day")]
    public async Task<ActionResult<DayViewDto>> Day([FromQuery] DateOnly date, [FromQuery] Guid? userId, CancellationToken ct)
    {
        if (date == default) date = DateOnly.FromDateTime(DateTime.Today);
        return Ok(await _daily.GetDayAsync(date, userId, ct));
    }

    [HttpGet("calendar")]
    public async Task<ActionResult<CalendarDto>> Calendar([FromQuery] int year, [FromQuery] int month, [FromQuery] Guid? userId, CancellationToken ct)
    {
        var today = DateTime.Today;
        if (year == 0) year = today.Year;
        if (month == 0) month = today.Month;
        return Ok(await _daily.GetCalendarAsync(year, month, userId, ct));
    }

    [HttpPost]
    public async Task<ActionResult<DailyEntryDto>> Create(SaveDailyEntryRequest request, CancellationToken ct)
    {
        var entry = await _daily.CreateAsync(request, ct);
        return Created($"/api/daily-work/{entry.Id}", entry);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DailyEntryDto>> Update(Guid id, SaveDailyEntryRequest request, CancellationToken ct)
        => Ok(await _daily.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _daily.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("submit")]
    public async Task<ActionResult<DayViewDto>> Submit(SubmitDayRequest request, CancellationToken ct)
        => Ok(await _daily.SubmitAsync(request.WorkDate, ct));

    [Authorize(Roles = Roles.TeamLead)]
    [HttpPost("reopen")]
    public async Task<ActionResult<DayViewDto>> Reopen(ReopenDayRequest request, CancellationToken ct)
        => Ok(await _daily.ReopenAsync(request, ct));
}

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet("my")]
    public async Task<ActionResult<MyDashboardDto>> My(CancellationToken ct)
        => Ok(await _dashboard.MyAsync(ct));

    [Authorize(Roles = Roles.TeamLead)]
    [HttpGet("team")]
    public async Task<ActionResult<TeamDashboardDto>> Team([FromQuery] DateOnly? date, CancellationToken ct)
        => Ok(await _dashboard.TeamAsync(date, ct));
}

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> List(CancellationToken ct)
        => Ok(await _notifications.GetAsync(ct));
}

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private const string Excel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private readonly IReportService _reports;
    private readonly IExportService _export;
    private readonly IDailyWorkService _daily;

    public ReportsController(IReportService reports, IExportService export, IDailyWorkService daily)
    {
        _reports = reports;
        _export = export;
        _daily = daily;
    }

    [HttpGet("monthly")]
    public async Task<ActionResult<MonthlyReportDto>> Monthly([FromQuery] int year, [FromQuery] int month, [FromQuery] Guid? userId, CancellationToken ct)
    {
        var today = DateTime.Today;
        if (year == 0) year = today.Year;
        if (month == 0) month = today.Month;
        return Ok(await _reports.MonthlyAsync(year, month, userId, ct));
    }

    [Authorize(Roles = Roles.TeamLead)]
    [HttpGet("team")]
    public async Task<ActionResult<TeamReportDto>> Team([FromQuery] int year, [FromQuery] int month, [FromQuery] bool includeSelf, CancellationToken ct)
    {
        var today = DateTime.Today;
        if (year == 0) year = today.Year;
        if (month == 0) month = today.Month;
        return Ok(await _reports.TeamAsync(year, month, includeSelf, ct));
    }

    [HttpGet("monthly/export")]
    public async Task<IActionResult> MonthlyExport([FromQuery] int year, [FromQuery] int month, [FromQuery] Guid? userId, [FromQuery] string format = "xlsx", CancellationToken ct = default)
    {
        var today = DateTime.Today;
        if (year == 0) year = today.Year;
        if (month == 0) month = today.Month;
        var report = await _reports.MonthlyAsync(year, month, userId, ct);
        var name = $"{Slug(report.Employee.Name)}-{year}-{month:00}";
        return format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? File(_export.MonthlyPdf(report), "application/pdf", $"{name}.pdf")
            : File(_export.MonthlyExcel(report), Excel, $"{name}.xlsx");
    }

    [Authorize(Roles = Roles.TeamLead)]
    [HttpGet("team/export")]
    public async Task<IActionResult> TeamExport([FromQuery] int year, [FromQuery] int month, [FromQuery] bool includeSelf, [FromQuery] string format = "xlsx", CancellationToken ct = default)
    {
        var today = DateTime.Today;
        if (year == 0) year = today.Year;
        if (month == 0) month = today.Month;
        var report = await _reports.TeamAsync(year, month, includeSelf, ct);
        var name = $"team-{year}-{month:00}";
        return format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? File(_export.TeamPdf(report), "application/pdf", $"{name}.pdf")
            : File(_export.TeamExcel(report), Excel, $"{name}.xlsx");
    }

    [HttpGet("daily/export")]
    public async Task<IActionResult> DailyExport([FromQuery] DateOnly date, [FromQuery] Guid? userId, [FromQuery] string format = "xlsx", CancellationToken ct = default)
    {
        if (date == default) date = DateOnly.FromDateTime(DateTime.Today);
        var day = await _daily.GetDayAsync(date, userId, ct);
        var name = $"{Slug(day.UserName)}-{date:yyyy-MM-dd}";
        return format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? File(_export.DailyPdf(day), "application/pdf", $"{name}.pdf")
            : File(_export.DailyExcel(day), Excel, $"{name}.xlsx");
    }

    private static string Slug(string value)
    {
        var chars = value.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        return new string(chars).Trim('-');
    }
}
