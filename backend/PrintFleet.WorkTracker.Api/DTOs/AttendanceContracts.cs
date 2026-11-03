namespace PrintFleet.WorkTracker.Api.DTOs;

public class AttendanceMonthDto
{
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public int Year { get; set; }
    public int Month { get; set; }
    public string RegisterStatus { get; set; } = "Draft";
    public Guid? RegisterId { get; set; }
    public bool CanEdit { get; set; }
    public bool CanReview { get; set; }
    public int AvailableWorkingDays { get; set; }
    public int HolidayCount { get; set; }
    public int ActiveEmployees { get; set; }
    public int UnmarkedWorkingDays { get; set; }
    public DateOnly PreparedOn { get; set; }
    public string? ReviewReason { get; set; }
    public IReadOnlyList<AttendanceDateDto> Dates { get; set; } = [];
    public IReadOnlyList<AttendanceEmployeeDto> Employees { get; set; } = [];
    public IReadOnlyList<string> Warnings { get; set; } = [];
}

public class AttendanceDateDto
{
    public DateOnly Date { get; set; }
    public bool Working { get; set; }
    public bool Holiday { get; set; }
    public string? HolidayName { get; set; }
}

public class AttendanceEmployeeDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string? EmployeeNumber { get; set; }
    public int Present { get; set; }
    public int Leave { get; set; }
    public int CompOff { get; set; }
    public int Od { get; set; }
    public int Absent { get; set; }
    public int Unmarked { get; set; }
    public int TotalWorked { get; set; }
    public DateOnly? LeaveFrom { get; set; }
    public DateOnly? LeaveTo { get; set; }
    public DateOnly? OdFrom { get; set; }
    public DateOnly? OdTo { get; set; }
    public string? Remarks { get; set; }
    public IReadOnlyList<AttendanceMarkDto> Marks { get; set; } = [];
}

public class AttendanceMarkDto
{
    public DateOnly Date { get; set; }
    public string Status { get; set; } = "";
    public string? Remarks { get; set; }
    public string? LeaveType { get; set; }
    public DateOnly? EarnedDate { get; set; }
}

public class SaveAttendanceDayRequest
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public string Status { get; set; } = "";
    public string? Remarks { get; set; }
    public string? LeaveType { get; set; }
    public DateOnly? EarnedDate { get; set; }
}

public class MarkPresentRequest
{
    public DateOnly Date { get; set; }
    public Guid? DepartmentId { get; set; }
}

public class SaveAttendanceRangeRequest
{
    public Guid UserId { get; set; }
    public string Kind { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? LeaveType { get; set; }
    public DateOnly? EarnedDate { get; set; }
    public string? Remarks { get; set; }
}

public class AttendanceDecisionRequest
{
    public Guid? DepartmentId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string? Reason { get; set; }
}

public class CompanyHolidayDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateOnly HolidayDate { get; set; }
    public string HolidayType { get; set; } = "";
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
}

public class SaveHolidayRequest
{
    public string Name { get; set; } = "";
    public DateOnly HolidayDate { get; set; }
    public string HolidayType { get; set; } = "Company";
    public Guid? DepartmentId { get; set; }
}

public class DepartmentCalendarDto
{
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public List<int> WeeklyOffs { get; set; } = [];
    public string AlternateSaturday { get; set; } = "";
}

public class SaveCalendarRequest
{
    public List<int> WeeklyOffs { get; set; } = [];
    public string AlternateSaturday { get; set; } = "";
}

public class AttendanceReportDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public IReadOnlyList<AttendanceDepartmentReportDto> Departments { get; set; } = [];
}

public class AttendanceDepartmentReportDto
{
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public string RegisterStatus { get; set; } = "";
    public int ActiveEmployees { get; set; }
    public int AvailableWorkingDays { get; set; }
    public int Present { get; set; }
    public int Leave { get; set; }
    public int CompOff { get; set; }
    public int Od { get; set; }
    public int Absent { get; set; }
    public int Unmarked { get; set; }
    public decimal CompletionPercent { get; set; }
}

public class AttendanceEmployeeNumberDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string? DepartmentName { get; set; }
    public string? EmployeeNumber { get; set; }
}

public class SaveEmployeeNumberRequest
{
    public string? EmployeeNumber { get; set; }
}

public class AttendanceCorrectionRequest
{
    public DateOnly Date { get; set; }
    public string RequestedStatus { get; set; } = "";
    public string Reason { get; set; } = "";
}

public class AttendanceCorrectionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
    public DateOnly Date { get; set; }
    public string RequestedStatus { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "";
}
