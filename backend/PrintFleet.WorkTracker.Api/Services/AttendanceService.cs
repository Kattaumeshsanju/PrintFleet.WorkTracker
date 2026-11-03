using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IAttendanceService
{
    Task<AttendanceMonthDto> MonthAsync(int year, int month, Guid? departmentId, CancellationToken ct);
    Task<AttendanceMonthDto> SaveDayAsync(SaveAttendanceDayRequest request, CancellationToken ct);
    Task<AttendanceMonthDto> MarkPresentAsync(MarkPresentRequest request, CancellationToken ct);
    Task<AttendanceMonthDto> SaveRangeAsync(SaveAttendanceRangeRequest request, CancellationToken ct);
    Task<AttendanceMonthDto> SubmitAsync(AttendanceDecisionRequest request, CancellationToken ct);
    Task<AttendanceMonthDto> ApproveAsync(AttendanceDecisionRequest request, CancellationToken ct);
    Task<AttendanceMonthDto> RejectAsync(AttendanceDecisionRequest request, CancellationToken ct);
    Task<AttendanceMonthDto> ReopenAsync(AttendanceDecisionRequest request, CancellationToken ct);
    Task<IReadOnlyList<CompanyHolidayDto>> HolidaysAsync(int year, CancellationToken ct);
    Task<CompanyHolidayDto> SaveHolidayAsync(SaveHolidayRequest request, CancellationToken ct);
    Task DeleteHolidayAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DepartmentCalendarDto>> CalendarsAsync(CancellationToken ct);
    Task<DepartmentCalendarDto> SaveCalendarAsync(Guid departmentId, SaveCalendarRequest request, CancellationToken ct);
    Task<AttendanceReportDto> ReportAsync(int year, int month, CancellationToken ct);
    Task<IReadOnlyList<AttendanceEmployeeNumberDto>> EmployeeNumbersAsync(CancellationToken ct);
    Task SetEmployeeNumberAsync(Guid userId, string? employeeNumber, CancellationToken ct);
    Task<AttendanceCorrectionDto> RequestCorrectionAsync(AttendanceCorrectionRequest request, CancellationToken ct);
    Task<IReadOnlyList<AttendanceCorrectionDto>> CorrectionsAsync(Guid? departmentId, CancellationToken ct);
    Task DecideCorrectionAsync(Guid id, bool approve, string? reason, CancellationToken ct);
    Task<byte[]> ExportAsync(int year, int month, Guid? departmentId, CancellationToken ct);
}

public class AttendanceService : IAttendanceService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IDepartmentAccess _access;

    public AttendanceService(AppDbContext db, ICurrentUser current, IDepartmentAccess access)
    {
        _db = db;
        _current = current;
        _access = access;
    }

    public async Task<AttendanceMonthDto> MonthAsync(int year, int month, Guid? departmentId, CancellationToken ct)
    {
        var department = await ResolveDepartmentAsync(departmentId, ct);
        return await BuildMonthAsync(department, year, month, ct);
    }

    public async Task<AttendanceMonthDto> SaveDayAsync(SaveAttendanceDayRequest request, CancellationToken ct)
    {
        var user = await EditableUserAsync(request.UserId, request.Date, ct);
        var existing = await _db.AttendanceDays.FirstOrDefaultAsync(x => x.UserId == user.Id && x.AttendanceDate == request.Date, ct);
        if (request.Status == "NotMarked")
        {
            if (existing is not null)
            {
                if (existing.DepartmentId != user.DepartmentId)
                    throw new AppException("This date was recorded in another department and is kept as history.");
                AddChange(existing.Id, null, user.Id, "Cleared", existing.Status, "NotMarked", request.Remarks);
                _db.AttendanceDays.Remove(existing);
                await _db.SaveChangesAsync(ct);
            }
            return await BuildMonthAsync(user.Department!, request.Date.Year, request.Date.Month, ct);
        }
        if (!AttendanceStatuses.All.Contains(request.Status))
            throw new AppException("Choose a valid attendance status.");
        if (existing is not null && existing.DepartmentId != user.DepartmentId)
            throw new AppException("This date was recorded in another department and is kept as history.");
        var previous = existing?.Status;
        if (existing is null)
        {
            existing = new AttendanceDay
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                DepartmentId = user.DepartmentId!.Value,
                AttendanceDate = request.Date,
                CreatedAt = DateTime.UtcNow,
                RecordedById = _current.UserId
            };
            _db.AttendanceDays.Add(existing);
        }
        existing.Status = request.Status;
        existing.Remarks = WorkMetrics.Clean(request.Remarks);
        existing.LeaveType = WorkMetrics.Clean(request.LeaveType);
        existing.EarnedDate = request.Status == AttendanceStatuses.CompOff ? request.EarnedDate : null;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.RecordedById = _current.UserId;
        existing.SpanId = null;
        AddChange(existing.Id, null, user.Id, previous is null ? "Created" : "Updated", previous, request.Status, null);
        await _db.SaveChangesAsync(ct);
        return await BuildMonthAsync(user.Department!, request.Date.Year, request.Date.Month, ct);
    }

    public async Task<AttendanceMonthDto> MarkPresentAsync(MarkPresentRequest request, CancellationToken ct)
    {
        var department = await EditableDepartmentAsync(request.DepartmentId, ct);
        await EnsureEditableAsync(department.Id, request.Date.Year, request.Date.Month, ct);
        var calendar = await CalendarAsync(department.Id, ct);
        var holidays = await HolidayDatesAsync(department.Id, request.Date.Year, ct);
        if (!AttendanceRules.IsWorkingDay(request.Date, calendar, holidays))
            throw new AppException("That date is a weekly off or a company holiday.");
        var people = await ActiveMembersAsync(department.Id, ct);
        var marked = await _db.AttendanceDays.Where(x => x.DepartmentId == department.Id && x.AttendanceDate == request.Date).Select(x => x.UserId).ToListAsync(ct);
        var markedIds = marked.ToHashSet();
        foreach (var person in people.Where(person => !markedIds.Contains(person.Id)))
        {
            var row = new AttendanceDay
            {
                Id = Guid.NewGuid(),
                UserId = person.Id,
                DepartmentId = department.Id,
                AttendanceDate = request.Date,
                Status = AttendanceStatuses.Present,
                RecordedById = _current.UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.AttendanceDays.Add(row);
            AddChange(row.Id, null, person.Id, "Created", null, AttendanceStatuses.Present, "Marked present");
        }
        await _db.SaveChangesAsync(ct);
        return await BuildMonthAsync(department, request.Date.Year, request.Date.Month, ct);
    }

    public async Task<AttendanceMonthDto> SaveRangeAsync(SaveAttendanceRangeRequest request, CancellationToken ct)
    {
        if (!AttendanceStatuses.Ranges.Contains(request.Kind))
            throw new AppException("A range can be leave, compensatory off, or outstation duty.");
        if (request.EndDate.Year != request.StartDate.Year || request.EndDate.Month != request.StartDate.Month)
            throw new AppException("A range must stay inside one month.");
        var user = await EditableUserAsync(request.UserId, request.StartDate, ct);
        var calendar = await CalendarAsync(user.DepartmentId!.Value, ct);
        var holidays = await HolidayDatesAsync(user.DepartmentId.Value, request.StartDate.Year, ct);
        var days = AttendanceRules.WorkingDaysInRange(request.StartDate, request.EndDate, calendar, holidays);
        if (days.Count == 0)
            throw new AppException("That range has no working days after weekly offs and holidays.");
        var existing = await _db.AttendanceDays.Where(x => x.UserId == user.Id && x.AttendanceDate >= request.StartDate && x.AttendanceDate <= request.EndDate).ToListAsync(ct);
        var otherDepartment = existing.FirstOrDefault(row => days.Contains(row.AttendanceDate) && row.DepartmentId != user.DepartmentId);
        if (otherDepartment is not null)
            throw new AppException($"Attendance on {otherDepartment.AttendanceDate:dd MMM yyyy} belongs to another department and is kept as history.");
        var conflict = existing.FirstOrDefault(row => days.Contains(row.AttendanceDate) && row.Status != request.Kind);
        if (conflict is not null)
            throw new AppException($"Attendance is already marked on {conflict.AttendanceDate:dd MMM yyyy}. Clear that day before applying this range.");
        var span = new AttendanceSpan
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DepartmentId = user.DepartmentId.Value,
            Kind = request.Kind,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            LeaveType = WorkMetrics.Clean(request.LeaveType),
            EarnedDate = request.Kind == AttendanceStatuses.CompOff ? request.EarnedDate : null,
            Remarks = WorkMetrics.Clean(request.Remarks),
            CreatedById = _current.UserId,
            CreatedAt = DateTime.UtcNow
        };
        _db.AttendanceSpans.Add(span);
        var have = existing.ToDictionary(row => row.AttendanceDate);
        foreach (var date in days)
        {
            if (!have.TryGetValue(date, out var row))
            {
                row = new AttendanceDay
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    DepartmentId = user.DepartmentId.Value,
                    AttendanceDate = date,
                    CreatedAt = DateTime.UtcNow,
                    RecordedById = _current.UserId
                };
                _db.AttendanceDays.Add(row);
            }
            row.Status = request.Kind;
            row.Remarks = span.Remarks;
            row.LeaveType = span.LeaveType;
            row.EarnedDate = span.EarnedDate;
            row.SpanId = span.Id;
            row.UpdatedAt = DateTime.UtcNow;
            row.RecordedById = _current.UserId;
        }
        AddChange(null, null, user.Id, "Range", null, $"{request.Kind} {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}", span.Remarks);
        await _db.SaveChangesAsync(ct);
        return await BuildMonthAsync(user.Department!, request.StartDate.Year, request.StartDate.Month, ct);
    }

    public Task<AttendanceMonthDto> SubmitAsync(AttendanceDecisionRequest request, CancellationToken ct)
        => ChangeRegisterAsync(request, AttendanceRegisterStatuses.Draft, AttendanceRegisterStatuses.Submitted, false, ct);

    public Task<AttendanceMonthDto> ApproveAsync(AttendanceDecisionRequest request, CancellationToken ct)
        => ChangeRegisterAsync(request, AttendanceRegisterStatuses.Submitted, AttendanceRegisterStatuses.Approved, true, ct);

    public Task<AttendanceMonthDto> RejectAsync(AttendanceDecisionRequest request, CancellationToken ct)
        => ChangeRegisterAsync(request, AttendanceRegisterStatuses.Submitted, AttendanceRegisterStatuses.Draft, true, ct);

    public async Task<AttendanceMonthDto> ReopenAsync(AttendanceDecisionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new AppException("A reason is required to reopen an approved register.");
        return await ChangeRegisterAsync(request, AttendanceRegisterStatuses.Approved, AttendanceRegisterStatuses.Draft, true, ct);
    }

    public async Task<IReadOnlyList<CompanyHolidayDto>> HolidaysAsync(int year, CancellationToken ct)
    {
        await EnsureReaderAsync(ct);
        var holidays = await _db.CompanyHolidays.AsNoTracking().Where(x => x.HolidayDate.Year == year).OrderBy(x => x.HolidayDate).ToListAsync(ct);
        var names = await _db.Departments.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return holidays.Select(x => new CompanyHolidayDto
        {
            Id = x.Id,
            Name = x.Name,
            HolidayDate = x.HolidayDate,
            HolidayType = x.HolidayType,
            DepartmentId = x.DepartmentId,
            DepartmentName = x.DepartmentId is null ? "All departments" : names.GetValueOrDefault(x.DepartmentId.Value)
        }).ToList();
    }

    public async Task<CompanyHolidayDto> SaveHolidayAsync(SaveHolidayRequest request, CancellationToken ct)
    {
        if (!_current.IsAdmin) throw new AppException("Only an admin can change the holiday calendar.", StatusCodes.Status403Forbidden);
        if (string.IsNullOrWhiteSpace(request.Name)) throw new AppException("A holiday needs a name.");
        if (await _db.CompanyHolidays.AnyAsync(x => x.HolidayDate == request.HolidayDate && x.DepartmentId == request.DepartmentId, ct))
            throw new AppException("That holiday is already on the calendar.");
        var row = new CompanyHoliday
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            HolidayDate = request.HolidayDate,
            HolidayType = string.IsNullOrWhiteSpace(request.HolidayType) ? "Company" : request.HolidayType.Trim(),
            DepartmentId = request.DepartmentId
        };
        _db.CompanyHolidays.Add(row);
        await _db.SaveChangesAsync(ct);
        return new CompanyHolidayDto { Id = row.Id, Name = row.Name, HolidayDate = row.HolidayDate, HolidayType = row.HolidayType, DepartmentId = row.DepartmentId };
    }

    public async Task DeleteHolidayAsync(Guid id, CancellationToken ct)
    {
        if (!_current.IsAdmin) throw new AppException("Only an admin can change the holiday calendar.", StatusCodes.Status403Forbidden);
        var row = await _db.CompanyHolidays.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException("Holiday was not found.", StatusCodes.Status404NotFound);
        _db.CompanyHolidays.Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DepartmentCalendarDto>> CalendarsAsync(CancellationToken ct)
    {
        if (!_current.IsAdmin) throw new AppException("Only an admin can view work calendars.", StatusCodes.Status403Forbidden);
        var departments = await _db.Departments.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        var calendars = await _db.DepartmentWorkCalendars.AsNoTracking().ToDictionaryAsync(x => x.DepartmentId, ct);
        return departments.Select(department => MapCalendar(department, calendars.GetValueOrDefault(department.Id))).ToList();
    }

    public async Task<DepartmentCalendarDto> SaveCalendarAsync(Guid departmentId, SaveCalendarRequest request, CancellationToken ct)
    {
        if (!_current.IsAdmin) throw new AppException("Only an admin can change a work calendar.", StatusCodes.Status403Forbidden);
        if (!AlternateSaturdayRules.All.Contains(request.AlternateSaturday))
            throw new AppException("Choose a valid Saturday arrangement.");
        var department = await _db.Departments.FirstOrDefaultAsync(x => x.Id == departmentId, ct)
            ?? throw new AppException("Department was not found.", StatusCodes.Status404NotFound);
        var row = await _db.DepartmentWorkCalendars.FirstOrDefaultAsync(x => x.DepartmentId == departmentId, ct);
        if (row is null)
        {
            row = new DepartmentWorkCalendar { DepartmentId = departmentId };
            _db.DepartmentWorkCalendars.Add(row);
        }
        row.WeeklyOffs = AttendanceRules.FormatOffs(request.WeeklyOffs.Where(day => day is >= 0 and <= 6).Select(day => (DayOfWeek)day));
        row.AlternateSaturday = request.AlternateSaturday;
        await _db.SaveChangesAsync(ct);
        return MapCalendar(department, row);
    }

    public async Task<AttendanceReportDto> ReportAsync(int year, int month, CancellationToken ct)
    {
        await EnsureReaderAsync(ct);
        var departments = await VisibleDepartmentsAsync(ct);
        var rows = new List<AttendanceDepartmentReportDto>();
        foreach (var department in departments)
        {
            var monthView = await BuildMonthAsync(department, year, month, ct);
            var employees = monthView.Employees;
            var slots = Math.Max(1, employees.Count * monthView.AvailableWorkingDays);
            var marked = employees.Sum(person => monthView.AvailableWorkingDays - person.Unmarked);
            rows.Add(new AttendanceDepartmentReportDto
            {
                DepartmentId = department.Id,
                DepartmentName = department.Name,
                RegisterStatus = monthView.RegisterStatus,
                ActiveEmployees = monthView.ActiveEmployees,
                AvailableWorkingDays = monthView.AvailableWorkingDays,
                Present = employees.Sum(person => person.Present),
                Leave = employees.Sum(person => person.Leave),
                CompOff = employees.Sum(person => person.CompOff),
                Od = employees.Sum(person => person.Od),
                Absent = employees.Sum(person => person.Absent),
                Unmarked = employees.Sum(person => person.Unmarked),
                CompletionPercent = employees.Count == 0 ? 0 : Math.Round(marked * 100m / slots, 1)
            });
        }
        return new AttendanceReportDto { Year = year, Month = month, Departments = rows };
    }

    public async Task<IReadOnlyList<AttendanceEmployeeNumberDto>> EmployeeNumbersAsync(CancellationToken ct)
    {
        if (!_current.IsAdmin) throw new AppException("Only an admin can maintain employee numbers.", StatusCodes.Status403Forbidden);
        return await _db.Users.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new AttendanceEmployeeNumberDto
            {
                UserId = x.Id,
                Name = x.Name,
                DepartmentName = x.Department == null ? null : x.Department.Name,
                EmployeeNumber = x.EmployeeNumber
            }).ToListAsync(ct);
    }

    public async Task SetEmployeeNumberAsync(Guid userId, string? employeeNumber, CancellationToken ct)
    {
        if (!_current.IsAdmin) throw new AppException("Only an admin can set an employee number.", StatusCodes.Status403Forbidden);
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);
        var number = WorkMetrics.Clean(employeeNumber);
        if (number is not null && await _db.Users.AnyAsync(x => x.EmployeeNumber == number && x.Id != userId, ct))
            throw new AppException("That employee number is already in use.", StatusCodes.Status409Conflict);
        user.EmployeeNumber = number;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AttendanceCorrectionDto> RequestCorrectionAsync(AttendanceCorrectionRequest request, CancellationToken ct)
    {
        if (!AttendanceStatuses.All.Contains(request.RequestedStatus))
            throw new AppException("Choose a valid attendance status.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new AppException("Explain the correction.");
        var user = await _db.Users.AsNoTracking().FirstAsync(x => x.Id == _current.UserId, ct);
        if (user.DepartmentId is null) throw new AppException("Your account is not in a department.");
        var register = await FindRegisterAsync(user.DepartmentId.Value, request.Date.Year, request.Date.Month, ct);
        if (register?.Status == AttendanceRegisterStatuses.Approved)
            throw new AppException("That month is approved. Ask an admin to reopen it before requesting a change.");
        var row = new AttendanceCorrection
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DepartmentId = user.DepartmentId.Value,
            AttendanceDate = request.Date,
            RequestedStatus = request.RequestedStatus,
            Reason = request.Reason.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _db.AttendanceCorrections.Add(row);
        await _db.SaveChangesAsync(ct);
        return new AttendanceCorrectionDto { Id = row.Id, UserId = user.Id, UserName = user.Name, Date = row.AttendanceDate, RequestedStatus = row.RequestedStatus, Reason = row.Reason, Status = row.Status };
    }

    public async Task<IReadOnlyList<AttendanceCorrectionDto>> CorrectionsAsync(Guid? departmentId, CancellationToken ct)
    {
        var department = _current.Role == Roles.TeamMember ? null : await ResolveDepartmentAsync(departmentId, ct);
        var query = _db.AttendanceCorrections.AsNoTracking().Where(x => x.Status == "Pending");
        query = _current.Role == Roles.TeamMember ? query.Where(x => x.UserId == _current.UserId) : query.Where(x => x.DepartmentId == department!.Id);
        return await query.OrderByDescending(x => x.CreatedAt)
            .Select(x => new AttendanceCorrectionDto
            {
                Id = x.Id,
                UserId = x.UserId,
                UserName = x.User.Name,
                Date = x.AttendanceDate,
                RequestedStatus = x.RequestedStatus,
                Reason = x.Reason,
                Status = x.Status
            }).ToListAsync(ct);
    }

    public async Task DecideCorrectionAsync(Guid id, bool approve, string? reason, CancellationToken ct)
    {
        var row = await _db.AttendanceCorrections.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Correction request was not found.", StatusCodes.Status404NotFound);
        if (row.UserId == _current.UserId)
            throw new AppException("You cannot decide your own correction.", StatusCodes.Status403Forbidden);
        await EditableDepartmentAsync(row.DepartmentId, ct);
        await EnsureEditableAsync(row.DepartmentId, row.AttendanceDate.Year, row.AttendanceDate.Month, ct);
        row.Status = approve ? "Approved" : "Rejected";
        row.DecidedById = _current.UserId;
        row.DecisionReason = WorkMetrics.Clean(reason);
        if (approve)
        {
            var day = await _db.AttendanceDays.FirstOrDefaultAsync(x => x.UserId == row.UserId && x.AttendanceDate == row.AttendanceDate, ct);
            var previous = day?.Status;
            if (day is null)
            {
                day = new AttendanceDay
                {
                    Id = Guid.NewGuid(),
                    UserId = row.UserId,
                    DepartmentId = row.DepartmentId,
                    AttendanceDate = row.AttendanceDate,
                    CreatedAt = DateTime.UtcNow,
                    RecordedById = _current.UserId
                };
                _db.AttendanceDays.Add(day);
            }
            day.Status = row.RequestedStatus;
            day.Remarks = row.Reason;
            day.UpdatedAt = DateTime.UtcNow;
            AddChange(day.Id, null, row.UserId, "Correction", previous, row.RequestedStatus, row.Reason);
        }
        await NotifyAsync([row.UserId], row.DepartmentId, approve ? "Attendance correction approved" : "Attendance correction rejected", $"{row.AttendanceDate:dd MMM yyyy}: {row.RequestedStatus}. {reason}", ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<byte[]> ExportAsync(int year, int month, Guid? departmentId, CancellationToken ct)
    {
        if (_current.Role == Roles.TeamMember)
            throw new AppException("Employees cannot export the department register.", StatusCodes.Status403Forbidden);
        var monthView = await MonthAsync(year, month, departmentId, ct);
        return AttendanceExport.Build(monthView);
    }

    private async Task<AttendanceMonthDto> ChangeRegisterAsync(AttendanceDecisionRequest request, string from, string to, bool adminOnly, CancellationToken ct)
    {
        if (adminOnly && !_current.IsAdmin)
            throw new AppException("Only an admin can review a monthly register.", StatusCodes.Status403Forbidden);
        var department = adminOnly ? await ResolveDepartmentAsync(request.DepartmentId, ct) : await EditableDepartmentAsync(request.DepartmentId, ct);
        var register = await EnsureRegisterAsync(department.Id, request.Year, request.Month, ct);
        if (register.Status != from)
            throw new AppException($"The register is {register.Status}. This action needs it to be {from}.");
        register.Status = to;
        register.ReviewedById = _current.UserId;
        register.ReviewedAt = DateTime.UtcNow;
        register.ReviewReason = WorkMetrics.Clean(request.Reason);
        if (to == AttendanceRegisterStatuses.Submitted)
        {
            register.SubmittedById = _current.UserId;
            register.SubmittedAt = DateTime.UtcNow;
            var admins = await _db.Users.Where(x => x.IsActive && x.Role == Roles.Admin).Select(x => x.Id).ToListAsync(ct);
            await NotifyAsync(admins, department.Id, "Attendance register submitted", $"{department.Name} submitted {request.Year}-{request.Month:00} for review.", ct);
        }
        else
        {
            var lead = await _db.Users.Where(x => x.IsActive && x.Role == Roles.TeamLead && x.DepartmentId == department.Id).Select(x => x.Id).FirstOrDefaultAsync(ct);
            if (lead != Guid.Empty)
                await NotifyAsync([lead], department.Id, to == AttendanceRegisterStatuses.Approved ? "Attendance register approved" : "Attendance register returned", $"{department.Name} {request.Year}-{request.Month:00}: {request.Reason}", ct);
        }
        AddChange(null, register.Id, _current.UserId, to, from, to, request.Reason);
        await _db.SaveChangesAsync(ct);
        return await BuildMonthAsync(department, request.Year, request.Month, ct);
    }

    private async Task<AttendanceMonthDto> BuildMonthAsync(Department department, int year, int month, CancellationToken ct)
    {
        if (month is < 1 or > 12) throw new AppException("Month must be between 1 and 12.");
        var selfOnly = _current.Role == Roles.TeamMember;
        var calendar = await CalendarAsync(department.Id, ct);
        var holidayRows = await _db.CompanyHolidays.AsNoTracking()
            .Where(x => x.HolidayDate.Year == year && x.HolidayDate.Month == month && (x.DepartmentId == null || x.DepartmentId == department.Id))
            .ToListAsync(ct);
        var holidayDates = holidayRows.Select(x => x.HolidayDate).ToHashSet();
        var working = AttendanceRules.WorkingDays(year, month, calendar, holidayDates);
        var register = await FindRegisterAsync(department.Id, year, month, ct);
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var marks = await _db.AttendanceDays.AsNoTracking()
            .Where(x => x.DepartmentId == department.Id && x.AttendanceDate >= start && x.AttendanceDate <= end)
            .ToListAsync(ct);
        var historicalIds = marks.Select(mark => mark.UserId).Distinct().ToList();
        var people = await _db.Users.AsNoTracking()
            .Where(x => historicalIds.Contains(x.Id) || (x.IsActive && x.DepartmentId == department.Id && (x.Role == Roles.TeamLead || x.Role == Roles.TeamMember)))
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
        if (selfOnly) people = people.Where(person => person.Id == _current.UserId).ToList();
        var ids = people.Select(person => person.Id).ToArray();
        var elsewhere = await _db.AttendanceDays.AsNoTracking()
            .Where(x => ids.Contains(x.UserId) && x.DepartmentId != department.Id && x.AttendanceDate >= start && x.AttendanceDate <= end)
            .Select(x => new { x.UserId, x.AttendanceDate })
            .ToListAsync(ct);
        var submissions = await _db.DailySubmissions.AsNoTracking()
            .Where(x => ids.Contains(x.UserId) && x.WorkDate >= start && x.WorkDate <= end)
            .Select(x => new { x.UserId, x.WorkDate, x.Status, x.DayType })
            .ToListAsync(ct);
        var minutes = await _db.DailyWorkEntries.AsNoTracking()
            .Where(x => ids.Contains(x.UserId) && x.WorkDate >= start && x.WorkDate <= end)
            .GroupBy(x => new { x.UserId, x.WorkDate })
            .Select(g => new { g.Key.UserId, g.Key.WorkDate, Minutes = g.Sum(x => x.DurationMinutes) })
            .ToListAsync(ct);
        var warnings = new List<string>();
        var employees = new List<AttendanceEmployeeDto>();
        foreach (var person in people)
        {
            var own = marks.Where(mark => mark.UserId == person.Id).Select(mark => (mark.AttendanceDate, mark.Status)).ToList();
            var held = elsewhere.Where(mark => mark.UserId == person.Id).Select(mark => mark.AttendanceDate).ToHashSet();
            var math = AttendanceRules.Summarize(working, own);
            var unmarked = Math.Max(0, math.Unmarked - held.Count(working.Contains));
            var leaveSpan = AttendanceRules.Span(own, AttendanceStatuses.Leave, AttendanceStatuses.CompOff);
            var odSpan = AttendanceRules.Span(own, AttendanceStatuses.Od);
            var remark = string.Join("; ", marks.Where(mark => mark.UserId == person.Id && !string.IsNullOrWhiteSpace(mark.Remarks)).Select(mark => mark.Remarks).Distinct());
            employees.Add(new AttendanceEmployeeDto
            {
                UserId = person.Id,
                Name = person.Name,
                EmployeeNumber = person.EmployeeNumber,
                Present = math.Present,
                Leave = math.Leave,
                CompOff = math.CompOff,
                Od = math.Od,
                Absent = math.Absent,
                Unmarked = unmarked,
                TotalWorked = math.TotalWorked,
                LeaveFrom = leaveSpan.From,
                LeaveTo = leaveSpan.To,
                OdFrom = odSpan.From,
                OdTo = odSpan.To,
                Remarks = string.IsNullOrWhiteSpace(remark) ? null : remark,
                Marks = marks.Where(mark => mark.UserId == person.Id).Select(mark => new AttendanceMarkDto
                {
                    Date = mark.AttendanceDate,
                    Status = mark.Status,
                    Remarks = mark.Remarks,
                    LeaveType = mark.LeaveType,
                    EarnedDate = mark.EarnedDate
                }).ToList()
            });
            if (string.IsNullOrWhiteSpace(person.EmployeeNumber))
                warnings.Add($"{person.Name} has no employee number.");
            var openDays = working.Where(day => !own.Any(mark => mark.AttendanceDate == day) && !held.Contains(day)).ToList();
            if (openDays.Count > 0)
                warnings.Add($"{person.Name} has {openDays.Count} unmarked working day{(openDays.Count == 1 ? "" : "s")}.");
            foreach (var day in held.Where(working.Contains))
                warnings.Add($"{person.Name} already has attendance on {day:dd MMM} from another department.");
            foreach (var mark in own.Where(mark => mark.Status == AttendanceStatuses.Present))
            {
                var submitted = submissions.Any(row => row.UserId == person.Id && row.WorkDate == mark.AttendanceDate && row.Status == SubmissionStatuses.Submitted);
                if (!submitted) warnings.Add($"{person.Name} is Present on {mark.AttendanceDate:dd MMM} with no submitted daily work.");
            }
            foreach (var mark in own.Where(mark => mark.Status is AttendanceStatuses.Leave or AttendanceStatuses.CompOff or AttendanceStatuses.Absent))
            {
                var submitted = submissions.Any(row => row.UserId == person.Id && row.WorkDate == mark.AttendanceDate && row.Status == SubmissionStatuses.Submitted);
                if (submitted) warnings.Add($"{person.Name} submitted daily work on {mark.AttendanceDate:dd MMM}, which is marked {mark.Status}.");
            }
            foreach (var mark in own.Where(mark => mark.Status == AttendanceStatuses.Od))
            {
                var logged = minutes.Any(row => row.UserId == person.Id && row.WorkDate == mark.AttendanceDate && row.Minutes > 0);
                if (!logged) warnings.Add($"{person.Name} is on outstation duty on {mark.AttendanceDate:dd MMM} with no logged time.");
            }
            foreach (var submission in submissions.Where(row => row.UserId == person.Id && row.DayType == AttendanceStatuses.Od))
            {
                var recorded = own.Any(mark => mark.AttendanceDate == submission.WorkDate && mark.Status == AttendanceStatuses.Od);
                if (!recorded) warnings.Add($"{person.Name} has an outstation duty day on {submission.WorkDate:dd MMM} without an OD attendance record.");
            }
        }
        foreach (var holiday in holidayRows)
        {
            foreach (var worked in minutes.Where(row => row.WorkDate == holiday.HolidayDate && row.Minutes > 0))
            {
                var person = people.FirstOrDefault(item => item.Id == worked.UserId);
                if (person is not null) warnings.Add($"{person.Name} logged time on {holiday.HolidayDate:dd MMM}, a declared holiday.");
            }
        }

        var locked = register is not null && AttendanceRules.RegisterIsLocked(register.Status, _current.IsAdmin);
        return new AttendanceMonthDto
        {
            DepartmentId = department.Id,
            DepartmentName = department.Name,
            Year = year,
            Month = month,
            RegisterStatus = register?.Status ?? AttendanceRegisterStatuses.Draft,
            RegisterId = register?.Id,
            CanEdit = AttendanceRules.CanEdit(_current.Role) && !locked,
            CanReview = AttendanceRules.CanReview(_current.Role),
            AvailableWorkingDays = working.Count,
            HolidayCount = holidayRows.Count,
            ActiveEmployees = people.Count(person => person.IsActive && person.DepartmentId == department.Id),
            UnmarkedWorkingDays = employees.Sum(person => person.Unmarked),
            PreparedOn = register?.PreparedOn ?? DateOnly.FromDateTime(DateTime.Today),
            ReviewReason = register?.ReviewReason,
            Dates = AttendanceRules.DaysInMonth(year, month).Select(date => new AttendanceDateDto
            {
                Date = date,
                Working = working.Contains(date),
                Holiday = holidayDates.Contains(date),
                HolidayName = holidayRows.FirstOrDefault(row => row.HolidayDate == date)?.Name
            }).ToList(),
            Employees = employees,
            Warnings = selfOnly ? warnings.Where(item => item.StartsWith(people.FirstOrDefault()?.Name ?? "\u0000", StringComparison.Ordinal)).Take(40).ToList() : warnings.Take(80).ToList()
        };
    }

    private async Task<Department> ResolveDepartmentAsync(Guid? requested, CancellationToken ct)
    {
        if (_current.Role == Roles.TeamMember)
        {
            var mine = await _db.Users.AsNoTracking().Include(x => x.Department).FirstAsync(x => x.Id == _current.UserId, ct);
            return mine.Department ?? throw new AppException("Your account is not in a department.");
        }
        if (_current.Role == Roles.TeamLead)
        {
            var mine = await _access.MyDepartmentAsync(ct) ?? throw new AppException("Your account is not in a department.");
            AttendanceRules.EnsureDepartment(_current.Role, mine, requested ?? mine);
            return await _db.Departments.FirstAsync(x => x.Id == mine, ct);
        }
        if (_current.Role == Roles.Ceo && !await _access.CanViewOrganizationOverviewAsync(ct))
            throw new AppException("You do not have attendance reporting access.", StatusCodes.Status403Forbidden);
        if (_current.Role is not (Roles.Admin or Roles.Ceo))
            throw new AppException("You cannot open attendance.", StatusCodes.Status403Forbidden);
        var id = requested ?? await _db.Departments.OrderBy(x => x.Name).Select(x => x.Id).FirstOrDefaultAsync(ct);
        if (id == Guid.Empty) throw new AppException("There is no department to show.");
        return await _db.Departments.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException("Department was not found.", StatusCodes.Status404NotFound);
    }

    private async Task<Department> EditableDepartmentAsync(Guid? requested, CancellationToken ct)
    {
        if (!AttendanceRules.CanEdit(_current.Role))
            throw new AppException("You cannot change attendance.", StatusCodes.Status403Forbidden);
        var department = await ResolveDepartmentAsync(requested, ct);
        return department;
    }

    private async Task<User> EditableUserAsync(Guid userId, DateOnly date, CancellationToken ct)
    {
        var user = await _db.Users.Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == userId && x.IsActive, ct)
            ?? throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);
        if (user.DepartmentId is null || user.Role is not (Roles.TeamLead or Roles.TeamMember))
            throw new AppException("That account is not an active department member.");
        await EditableDepartmentAsync(user.DepartmentId, ct);
        if (_current.Role == Roles.TeamLead && user.DepartmentId != await _access.MyDepartmentAsync(ct))
            throw new AppException("You can record attendance only for your own department.", StatusCodes.Status403Forbidden);
        await EnsureEditableAsync(user.DepartmentId.Value, date.Year, date.Month, ct);
        return user;
    }

    private async Task EnsureEditableAsync(Guid departmentId, int year, int month, CancellationToken ct)
    {
        var register = await FindRegisterAsync(departmentId, year, month, ct);
        if (register is not null && AttendanceRules.RegisterIsLocked(register.Status, _current.IsAdmin))
            throw new AppException("This month is locked. An admin must reopen it before attendance can change.");
    }

    private async Task<MonthlyAttendanceRegister> EnsureRegisterAsync(Guid departmentId, int year, int month, CancellationToken ct)
    {
        var register = await FindRegisterAsync(departmentId, year, month, ct);
        if (register is not null) return register;
        register = new MonthlyAttendanceRegister
        {
            Id = Guid.NewGuid(),
            DepartmentId = departmentId,
            Year = year,
            Month = month,
            PreparedOn = DateOnly.FromDateTime(DateTime.Today)
        };
        _db.MonthlyAttendanceRegisters.Add(register);
        await _db.SaveChangesAsync(ct);
        return register;
    }

    private Task<MonthlyAttendanceRegister?> FindRegisterAsync(Guid departmentId, int year, int month, CancellationToken ct)
        => _db.MonthlyAttendanceRegisters.FirstOrDefaultAsync(x => x.DepartmentId == departmentId && x.Year == year && x.Month == month, ct);

    private async Task<List<User>> ActiveMembersAsync(Guid departmentId, CancellationToken ct)
        => await _db.Users.AsNoTracking()
            .Where(x => x.IsActive && x.DepartmentId == departmentId && (x.Role == Roles.TeamLead || x.Role == Roles.TeamMember))
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    private async Task<List<Department>> VisibleDepartmentsAsync(CancellationToken ct)
    {
        if (_current.Role is Roles.TeamLead or Roles.TeamMember)
        {
            var mine = _current.Role == Roles.TeamLead
                ? await _access.MyDepartmentAsync(ct)
                : await _db.Users.AsNoTracking().Where(x => x.Id == _current.UserId).Select(x => x.DepartmentId).FirstAsync(ct);
            if (mine is null) throw new AppException("Your account is not in a department.");
            return await _db.Departments.Where(x => x.Id == mine).ToListAsync(ct);
        }
        if (_current.Role == Roles.Ceo && !await _access.CanViewOrganizationOverviewAsync(ct))
            throw new AppException("You do not have attendance reporting access.", StatusCodes.Status403Forbidden);
        return await _db.Departments.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(ct);
    }

    private async Task<WorkCalendarConfig> CalendarAsync(Guid departmentId, CancellationToken ct)
    {
        var row = await _db.DepartmentWorkCalendars.AsNoTracking().FirstOrDefaultAsync(x => x.DepartmentId == departmentId, ct);
        return row is null ? AttendanceRules.Default : AttendanceRules.Parse(row.WeeklyOffs, row.AlternateSaturday);
    }

    private async Task<HashSet<DateOnly>> HolidayDatesAsync(Guid departmentId, int year, CancellationToken ct)
        => (await _db.CompanyHolidays.AsNoTracking()
            .Where(x => x.HolidayDate.Year == year && (x.DepartmentId == null || x.DepartmentId == departmentId))
            .Select(x => x.HolidayDate)
            .ToListAsync(ct)).ToHashSet();

    private async Task EnsureReaderAsync(CancellationToken ct)
    {
        if (_current.Role is Roles.Admin or Roles.TeamLead or Roles.TeamMember) return;
        if (_current.Role == Roles.Ceo && await _access.CanViewOrganizationOverviewAsync(ct)) return;
        throw new AppException("You cannot view attendance.", StatusCodes.Status403Forbidden);
    }

    private static DepartmentCalendarDto MapCalendar(Department department, DepartmentWorkCalendar? row)
    {
        var config = row is null ? AttendanceRules.Default : AttendanceRules.Parse(row.WeeklyOffs, row.AlternateSaturday);
        return new DepartmentCalendarDto
        {
            DepartmentId = department.Id,
            DepartmentName = department.Name,
            WeeklyOffs = config.WeeklyOffs.Select(day => (int)day).Order().ToList(),
            AlternateSaturday = config.AlternateSaturday
        };
    }

    private void AddChange(Guid? dayId, Guid? registerId, Guid userId, string action, string? previous, string? next, string? reason)
    {
        _db.AttendanceChanges.Add(new AttendanceChange
        {
            Id = Guid.NewGuid(),
            AttendanceDayId = dayId,
            RegisterId = registerId,
            UserId = userId,
            Action = action,
            PreviousValue = previous,
            NewValue = next,
            ChangedById = _current.UserId,
            Reason = WorkMetrics.Clean(reason),
            ChangedAt = DateTime.UtcNow
        });
    }

    private async Task NotifyAsync(IReadOnlyCollection<Guid> userIds, Guid departmentId, string title, string body, CancellationToken ct)
    {
        var ids = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return;
        var notice = new TeamNotice
        {
            Id = Guid.NewGuid(),
            DepartmentId = departmentId,
            SenderId = _current.UserId,
            Title = title,
            Body = body,
            Category = NoticeCategories.General,
            Channel = NoticeChannels.InApp,
            LinkPath = "/attendance",
            CreatedAt = DateTime.UtcNow
        };
        var emails = await _db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Email, ct);
        foreach (var id in ids)
            notice.Recipients.Add(new TeamNoticeRecipient { Id = Guid.NewGuid(), NoticeId = notice.Id, UserId = id, Email = emails.GetValueOrDefault(id) ?? "" });
        _db.TeamNotices.Add(notice);
    }
}

public static class AttendanceExport
{
    public static byte[] Build(AttendanceMonthDto month)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Attendance");
        sheet.Cell(1, 1).Value = "Monthly Employee Attendance & Leave Details";
        sheet.Range(1, 1, 1, 12).Merge().Style.Font.SetBold().Font.SetFontSize(16);
        sheet.Cell(2, 1).Value = "Month";
        sheet.Cell(2, 2).Value = new DateOnly(month.Year, month.Month, 1).ToString("MMMM yyyy");
        sheet.Cell(2, 4).Value = "Section / Department";
        sheet.Cell(2, 5).Value = month.DepartmentName;
        sheet.Cell(2, 8).Value = "Date";
        sheet.Cell(2, 9).Value = month.PreparedOn.ToString("dd MMM yyyy");
        sheet.Cell(3, 1).Value = "Total working days in the month";
        sheet.Cell(3, 3).Value = month.AvailableWorkingDays;
        sheet.Cell(3, 5).Value = "Number of holidays";
        sheet.Cell(3, 6).Value = month.HolidayCount;
        sheet.Cell(3, 8).Value = "Active employees";
        sheet.Cell(3, 9).Value = month.ActiveEmployees;
        var headers = new[]
        {
            "SL No.", "Employee Number", "Employee Name", "Present Days",
            "Leave / Compensatory Off Dates - From", "Leave / Compensatory Off Dates - To", "Number of Days of Leave",
            "Outstation Duty (OD) - From", "Outstation Duty (OD) - To", "Total Working Days", "Remarks / Compensatory Off in lieu", "Missing number"
        };
        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(5, column + 1).Value = headers[column];
        sheet.Range(5, 1, 5, headers.Length).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#1A4F7A")).Font.SetFontColor(XLColor.White);
        var row = 6;
        var serial = 1;
        foreach (var person in month.Employees)
        {
            sheet.Cell(row, 1).Value = serial++;
            sheet.Cell(row, 2).Value = person.EmployeeNumber ?? "";
            sheet.Cell(row, 3).Value = person.Name;
            sheet.Cell(row, 4).Value = person.Present;
            sheet.Cell(row, 5).Value = person.LeaveFrom?.ToString("dd MMM yyyy") ?? "";
            sheet.Cell(row, 6).Value = person.LeaveTo?.ToString("dd MMM yyyy") ?? "";
            sheet.Cell(row, 7).Value = person.Leave + person.CompOff;
            sheet.Cell(row, 8).Value = person.OdFrom?.ToString("dd MMM yyyy") ?? "";
            sheet.Cell(row, 9).Value = person.OdTo?.ToString("dd MMM yyyy") ?? "";
            sheet.Cell(row, 10).Value = person.TotalWorked;
            var comp = person.Marks.Where(mark => mark.Status == AttendanceStatuses.CompOff && mark.EarnedDate is not null)
                .Select(mark => $"C-off {mark.Date:dd MMM} in lieu of {mark.EarnedDate:dd MMM}");
            sheet.Cell(row, 11).Value = string.Join("; ", new[] { person.Remarks }.Concat(comp).Where(text => !string.IsNullOrWhiteSpace(text)));
            sheet.Cell(row, 12).Value = string.IsNullOrWhiteSpace(person.EmployeeNumber) ? "Missing employee number" : "";
            row++;
        }
        row += 2;
        sheet.Cell(row, 1).Value = "Guidelines";
        sheet.Cell(row, 1).Style.Font.SetBold();
        var guidelines = new[]
        {
            "Total Working Days = Present Days + Outstation Duty days.",
            "Available working days exclude weekly offs, configured alternate Saturdays, and company holidays.",
            "Leave and compensatory off are counted only on working days and are not Present days.",
            "A day with no attendance mark is unmarked. It is not recorded as absent.",
            "Daily work time and submitted timesheets are separate from attendance.",
            "This sheet is filled from WorkTracker attendance records for the selected department and month."
        };
        foreach (var line in guidelines)
        {
            row++;
            sheet.Cell(row, 1).Value = line;
            sheet.Range(row, 1, row, 8).Merge();
        }
        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(5);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
