using PrintFleet.WorkTracker.Api.Models;
using PrintFleet.WorkTracker.Api.Services;
using Xunit;

namespace PrintFleet.WorkTracker.Api.Tests;

public class AttendanceRulesTests
{
    private static readonly WorkCalendarConfig Calendar = AttendanceRules.Default;

    [Fact]
    public void Sundays_alternate_Saturdays_and_holidays_are_not_working_days()
    {
        var holiday = new DateOnly(2026, 11, 2);
        var holidays = new HashSet<DateOnly> { holiday };
        var working = AttendanceRules.WorkingDays(2026, 11, Calendar, holidays);
        Assert.DoesNotContain(new DateOnly(2026, 11, 1), working);
        Assert.DoesNotContain(holiday, working);
        Assert.DoesNotContain(new DateOnly(2026, 11, 14), working);
        Assert.DoesNotContain(new DateOnly(2026, 11, 28), working);
        Assert.Contains(new DateOnly(2026, 11, 7), working);
        Assert.Contains(new DateOnly(2026, 11, 3), working);
    }

    [Fact]
    public void Total_worked_is_present_plus_outstation_duty_and_unmarked_is_not_absent()
    {
        var working = new[] { new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 4), new DateOnly(2026, 11, 5) };
        var math = AttendanceRules.Summarize(working,
        [
            (working[0], AttendanceStatuses.Present),
            (working[1], AttendanceStatuses.Od),
            (working[2], AttendanceStatuses.Leave)
        ]);
        Assert.Equal(2, math.TotalWorked);
        Assert.Equal(1, math.Present);
        Assert.Equal(1, math.Od);
        Assert.Equal(1, math.Leave);
        Assert.Equal(0, math.Absent);
        Assert.Equal(1, math.Unmarked);
    }

    [Fact]
    public void Leave_on_a_holiday_and_compensatory_off_do_not_count_as_present()
    {
        var working = new[] { new DateOnly(2026, 11, 3) };
        var holiday = new DateOnly(2026, 11, 2);
        var math = AttendanceRules.Summarize(working,
        [
            (holiday, AttendanceStatuses.Leave),
            (working[0], AttendanceStatuses.CompOff)
        ]);
        Assert.Equal(0, math.Leave);
        Assert.Equal(0, math.Present);
        Assert.Equal(1, math.CompOff);
        Assert.Equal(0, math.TotalWorked);
    }

    [Fact]
    public void A_team_lead_cannot_open_another_department()
    {
        var own = Guid.NewGuid();
        var other = Guid.NewGuid();
        Assert.Throws<AppException>(() => AttendanceRules.EnsureDepartment(Roles.TeamLead, own, other));
        AttendanceRules.EnsureDepartment(Roles.TeamLead, own, own);
        AttendanceRules.EnsureDepartment(Roles.Admin, null, other);
        Assert.False(AttendanceRules.CanEdit(Roles.Ceo));
        Assert.False(AttendanceRules.CanEdit(Roles.TeamMember));
        Assert.True(AttendanceRules.RegisterIsLocked(AttendanceRegisterStatuses.Approved, true));
        Assert.True(AttendanceRules.RegisterIsLocked(AttendanceRegisterStatuses.Submitted, false));
        Assert.False(AttendanceRules.RegisterIsLocked(AttendanceRegisterStatuses.Submitted, true));
    }
}
