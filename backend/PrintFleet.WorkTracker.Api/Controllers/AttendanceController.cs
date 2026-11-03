using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Services;

namespace PrintFleet.WorkTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendance;

    public AttendanceController(IAttendanceService attendance) => _attendance = attendance;

    [HttpGet("month")]
    public Task<AttendanceMonthDto> Month([FromQuery] int year, [FromQuery] int month, [FromQuery] Guid? departmentId, CancellationToken ct)
        => _attendance.MonthAsync(year == 0 ? DateTime.Today.Year : year, month == 0 ? DateTime.Today.Month : month, departmentId, ct);

    [HttpPut("day")]
    public Task<AttendanceMonthDto> SaveDay(SaveAttendanceDayRequest request, CancellationToken ct) => _attendance.SaveDayAsync(request, ct);

    [HttpPost("present")]
    public Task<AttendanceMonthDto> Present(MarkPresentRequest request, CancellationToken ct) => _attendance.MarkPresentAsync(request, ct);

    [HttpPost("ranges")]
    public Task<AttendanceMonthDto> Range(SaveAttendanceRangeRequest request, CancellationToken ct) => _attendance.SaveRangeAsync(request, ct);

    [HttpPost("submit")]
    public Task<AttendanceMonthDto> Submit(AttendanceDecisionRequest request, CancellationToken ct) => _attendance.SubmitAsync(request, ct);

    [HttpPost("approve")]
    public Task<AttendanceMonthDto> Approve(AttendanceDecisionRequest request, CancellationToken ct) => _attendance.ApproveAsync(request, ct);

    [HttpPost("reject")]
    public Task<AttendanceMonthDto> Reject(AttendanceDecisionRequest request, CancellationToken ct) => _attendance.RejectAsync(request, ct);

    [HttpPost("reopen")]
    public Task<AttendanceMonthDto> Reopen(AttendanceDecisionRequest request, CancellationToken ct) => _attendance.ReopenAsync(request, ct);

    [HttpGet("holidays")]
    public Task<IReadOnlyList<CompanyHolidayDto>> Holidays([FromQuery] int year, CancellationToken ct)
        => _attendance.HolidaysAsync(year == 0 ? DateTime.Today.Year : year, ct);

    [HttpPost("holidays")]
    public Task<CompanyHolidayDto> SaveHoliday(SaveHolidayRequest request, CancellationToken ct) => _attendance.SaveHolidayAsync(request, ct);

    [HttpDelete("holidays/{id:guid}")]
    public async Task<IActionResult> DeleteHoliday(Guid id, CancellationToken ct)
    {
        await _attendance.DeleteHolidayAsync(id, ct);
        return NoContent();
    }

    [HttpGet("calendars")]
    public Task<IReadOnlyList<DepartmentCalendarDto>> Calendars(CancellationToken ct) => _attendance.CalendarsAsync(ct);

    [HttpPut("calendars/{departmentId:guid}")]
    public Task<DepartmentCalendarDto> SaveCalendar(Guid departmentId, SaveCalendarRequest request, CancellationToken ct)
        => _attendance.SaveCalendarAsync(departmentId, request, ct);

    [HttpGet("reports")]
    public Task<AttendanceReportDto> Report([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => _attendance.ReportAsync(year == 0 ? DateTime.Today.Year : year, month == 0 ? DateTime.Today.Month : month, ct);

    [HttpGet("employees")]
    public Task<IReadOnlyList<AttendanceEmployeeNumberDto>> Employees(CancellationToken ct) => _attendance.EmployeeNumbersAsync(ct);

    [HttpPut("employees/{id:guid}/number")]
    public async Task<IActionResult> Number(Guid id, SaveEmployeeNumberRequest request, CancellationToken ct)
    {
        await _attendance.SetEmployeeNumberAsync(id, request.EmployeeNumber, ct);
        return NoContent();
    }

    [HttpGet("corrections")]
    public Task<IReadOnlyList<AttendanceCorrectionDto>> Corrections([FromQuery] Guid? departmentId, CancellationToken ct)
        => _attendance.CorrectionsAsync(departmentId, ct);

    [HttpPost("corrections")]
    public Task<AttendanceCorrectionDto> RequestCorrection(AttendanceCorrectionRequest request, CancellationToken ct)
        => _attendance.RequestCorrectionAsync(request, ct);

    [HttpPost("corrections/{id:guid}/decide")]
    public async Task<IActionResult> Decide(Guid id, [FromQuery] bool approve, [FromQuery] string? reason, CancellationToken ct)
    {
        await _attendance.DecideCorrectionAsync(id, approve, reason, ct);
        return NoContent();
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] int year, [FromQuery] int month, [FromQuery] Guid? departmentId, CancellationToken ct)
    {
        var bytes = await _attendance.ExportAsync(year == 0 ? DateTime.Today.Year : year, month == 0 ? DateTime.Today.Month : month, departmentId, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"attendance-{year}-{month:00}.xlsx");
    }
}
