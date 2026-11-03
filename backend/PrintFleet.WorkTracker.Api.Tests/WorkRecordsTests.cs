using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PrintFleet.WorkTracker.Api.Middleware;
using PrintFleet.WorkTracker.Api.Models;
using PrintFleet.WorkTracker.Api.Services;
using Xunit;

namespace PrintFleet.WorkTracker.Api.Tests;

public class WorkRecordsTests
{
    [Fact]
    public void A_team_lead_sees_calendar_hour_totals_and_not_another_persons_daily_text()
    {
        var lead = Guid.NewGuid();
        var member = Guid.NewGuid();
        Assert.False(DepartmentRules.CanViewDaily(lead, Roles.TeamLead, member));
        Assert.True(DepartmentRules.CanViewDaily(member, Roles.TeamMember, member));
        Assert.Equal(9m, WorkRecords.OverviewActualHours([300, 240]));
        Assert.Equal(0m, WorkRecords.OverviewActualHours([]));
    }

    [Fact]
    public void A_sheet_keeps_the_department_it_was_opened_in()
    {
        var machine = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var application = Guid.Parse("20000000-0000-0000-0000-000000000002");
        Assert.Equal(machine, WorkRecords.ReportingDepartment(machine, application));
        Assert.Equal(application, WorkRecords.ReportingDepartment(null, application));
    }

    [Fact]
    public void Duplicate_goals_on_the_same_project_are_rejected_and_cancelled_copies_are_not()
    {
        var project = Guid.NewGuid();
        (string Title, Guid? ProjectId, string Status)[] existing =
        [
            ("Ship the release", project, TaskStatuses.InProgress),
            ("Old copy", project, TaskStatuses.Cancelled)
        ];
        Assert.True(WorkRecords.IsDuplicateGoal(" ship the release ", project, existing));
        Assert.False(WorkRecords.IsDuplicateGoal("Ship the release", Guid.NewGuid(), existing));
        Assert.False(WorkRecords.IsDuplicateGoal("Old copy", project, existing));
    }

    [Fact]
    public void Logged_hours_do_not_complete_a_goal()
    {
        var today = new DateOnly(2026, 10, 26);
        var stillOpen = WorkRecords.ApplyGoalProgress(TaskStatuses.Planned, TaskStatuses.InProgress, 25, null, null, today);
        Assert.Equal(25, stillOpen.ProgressPercent);
        Assert.Equal(today, stillOpen.ActualStart);
        Assert.Null(stillOpen.ActualEnd);

        var completed = WorkRecords.ApplyGoalProgress(TaskStatuses.InProgress, TaskStatuses.Completed, 25, today, null, today);
        Assert.Equal(100, completed.ProgressPercent);
        Assert.Equal(today, completed.ActualEnd);

        var reopened = WorkRecords.ApplyGoalProgress(TaskStatuses.Completed, TaskStatuses.InProgress, 80, today, today, today);
        Assert.Equal(80, reopened.ProgressPercent);
        Assert.Null(reopened.ActualEnd);
    }

    [Fact]
    public void A_project_with_goals_cannot_change_department_and_cancelled_projects_take_no_new_goals()
    {
        Assert.False(WorkRecords.CanChangeProjectDepartment(hasMonthlyGoals: true));
        Assert.True(WorkRecords.CanChangeProjectDepartment(hasMonthlyGoals: false));
        Assert.True(WorkRecords.CanAssignProject(ProjectStatuses.Active));
        Assert.True(WorkRecords.CanAssignProject(ProjectStatuses.OnHold));
        Assert.False(WorkRecords.CanAssignProject(ProjectStatuses.Cancelled));
        Assert.False(WorkRecords.CanAssignProject(ProjectStatuses.Completed));
    }

    [Fact]
    public void Reminder_failures_can_be_retried_without_storing_a_secret()
    {
        var day = new DateOnly(2026, 10, 26);
        var details = ReminderLedger.Describe(day, sent: 4, skipped: 1, failed: ["one@company.com"]);
        Assert.DoesNotContain("password", details, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["one@company.com"], ReminderLedger.FailedAddresses(details));
        Assert.Empty(ReminderLedger.FailedAddresses(ReminderLedger.Describe(day, 5, 1, [])));
    }

    [Fact]
    public async Task A_concurrent_save_is_rejected_as_a_conflict()
    {
        var middleware = new ExceptionMiddleware(_ => throw new DbUpdateConcurrencyException(), NullLogger<ExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        await middleware.Invoke(context);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }
}
