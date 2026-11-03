using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;
using Xunit;
using PrintFleet.WorkTracker.Api.Services;

namespace PrintFleet.WorkTracker.Api.Tests;

public class DepartmentRulesTests
{
    private static readonly Guid Actor = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Other = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid Machine = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid Application = Guid.Parse("20000000-0000-0000-0000-000000000002");

    [Fact]
    public void Daily_work_is_visible_to_the_owner_and_the_admin_only()
    {
        Assert.True(DepartmentRules.CanViewDaily(Actor, Roles.TeamMember, Actor));
        Assert.True(DepartmentRules.CanViewDaily(Actor, Roles.Admin, Other));
        Assert.True(DepartmentRules.CanViewDaily(Actor, Roles.Ceo, Other));
        Assert.False(DepartmentRules.CanViewDaily(Actor, Roles.TeamLead, Other));
        Assert.False(DepartmentRules.CanViewDaily(Actor, Roles.TeamMember, Other));
    }

    [Fact]
    public void The_ceo_can_review_the_organization_without_editing_it()
    {
        Assert.True(DepartmentRules.CanViewOrganization(Roles.Admin));
        Assert.True(DepartmentRules.CanViewOrganization(Roles.Ceo));
        Assert.False(DepartmentRules.CanViewOrganization(Roles.TeamLead));
        Assert.True(DepartmentRules.CanEditDepartmentWork(Roles.Admin));
        Assert.True(DepartmentRules.CanEditDepartmentWork(Roles.TeamLead));
        Assert.False(DepartmentRules.CanEditDepartmentWork(Roles.Ceo));
        Assert.False(DepartmentRules.CanAssignRole(true, Roles.Ceo));
        Assert.True(AccessRules.Overview(Roles.Ceo, false));
        Assert.True(AccessRules.Reports(Roles.Admin, false));
        Assert.True(AccessRules.Overview(Roles.TeamMember, true));
        Assert.True(AccessRules.Reports(Roles.TeamLead, true));
        Assert.False(AccessRules.Overview(Roles.TeamMember, false));
        Assert.False(AccessRules.Reports(Roles.TeamLead, false));
        Assert.True(AccessRules.Research(Roles.Ceo));
        Assert.False(AccessRules.Research(Roles.TeamMember));
        Assert.True(AccessRules.CanReceiveGrant(Roles.TeamMember));
        Assert.False(AccessRules.CanReceiveGrant(Roles.Ceo));
        Assert.False(AccessRules.CanReceiveGrant(Roles.Admin));
        Assert.True(AccessRules.CanManageAccess(Roles.Admin));
        Assert.False(AccessRules.CanManageAccess(Roles.Ceo));
        Assert.False(AccessRules.CanManageAccess(Roles.TeamLead));
    }

    [Fact]
    public void Only_an_admin_can_assign_a_team_lead()
    {
        Assert.True(DepartmentRules.CanAssignRole(true, Roles.TeamLead));
        Assert.True(DepartmentRules.CanAssignRole(false, Roles.TeamMember));
        Assert.False(DepartmentRules.CanAssignRole(false, Roles.TeamLead));
        Assert.False(DepartmentRules.CanAssignRole(true, Roles.Admin));
    }

    [Fact]
    public void A_department_can_have_only_one_team_lead()
    {
        Assert.True(DepartmentRules.HasSingleOrNoLead([Roles.TeamMember, Roles.TeamLead]));
        Assert.True(DepartmentRules.HasSingleOrNoLead([Roles.TeamMember]));
        Assert.True(DepartmentRules.HasSingleOrNoLead([]));
        Assert.False(DepartmentRules.HasSingleOrNoLead([Roles.TeamLead, Roles.TeamLead]));
    }

    [Fact]
    public void Only_an_admin_can_change_who_leads_a_department()
    {
        Assert.True(DepartmentRules.CanChangeTeamLead(true, Actor, Other));
        Assert.True(DepartmentRules.CanChangeTeamLead(false, Actor, Actor));
        Assert.True(DepartmentRules.CanChangeTeamLead(true, null, null));
        Assert.False(DepartmentRules.CanChangeTeamLead(false, Actor, Other));
        Assert.False(DepartmentRules.CanChangeTeamLead(false, Actor, null));
        Assert.False(DepartmentRules.CanChangeTeamLead(false, null, Other));
    }

    [Fact]
    public void A_team_lead_cannot_pull_someone_from_another_department()
    {
        Assert.True(DepartmentRules.CanMoveIntoDepartment(true, null, Machine, Application));
        Assert.True(DepartmentRules.CanMoveIntoDepartment(false, Machine, Machine, Machine));
        Assert.True(DepartmentRules.CanMoveIntoDepartment(false, Machine, null, Machine));
        Assert.False(DepartmentRules.CanMoveIntoDepartment(false, Machine, Application, Machine));
        Assert.False(DepartmentRules.CanMoveIntoDepartment(false, Machine, null, Application));
        Assert.False(DepartmentRules.CanMoveIntoDepartment(false, Machine, Machine, Application));
    }

    [Fact]
    public void Executive_progress_ignores_cancelled_goals_and_names_project_risk()
    {
        var today = new DateOnly(2026, 10, 20);
        Assert.True(ExecutiveMetrics.CountsTowardPlan(TaskStatuses.Planned));
        Assert.False(ExecutiveMetrics.CountsTowardPlan(TaskStatuses.Cancelled));
        Assert.Null(ExecutiveMetrics.Completion(0, 0));
        Assert.Equal(50m, ExecutiveMetrics.Completion(1, 2));
        var counts = UserService.CountTasks(
        [
            new MonthlyTaskDto { Status = TaskStatuses.Completed, EstimatedHours = 8, Title = "Done" },
            new MonthlyTaskDto { Status = TaskStatuses.Cancelled, EstimatedHours = 40, Title = "Dropped" }
        ]);
        Assert.Equal(1, counts.Total);
        Assert.Equal(1, counts.Completed);
        Assert.Equal(1, counts.Cancelled);
        Assert.Equal(8, counts.PlannedHours);
        Assert.Equal(100m, counts.Completion);
        Assert.Equal(0m, UserService.CountTasks([new MonthlyTaskDto { Status = TaskStatuses.Cancelled, EstimatedHours = 4, Title = "Only cancelled" }]).Completion);
        Assert.Equal(12.5m, UserService.CountTasks(
        [
            new MonthlyTaskDto { Status = TaskStatuses.InProgress, EstimatedHours = 10, ProgressPercent = 50, Title = "Half" },
            new MonthlyTaskDto { Status = TaskStatuses.Planned, EstimatedHours = 30, ProgressPercent = 0, Title = "Later" }
        ]).Completion);
        Assert.Equal(40m, WorkRecords.PlanCompletion(
        [
            (TaskStatuses.Planned, 80, 0m),
            (TaskStatuses.Planned, 0, 0m)
        ]));
        Assert.True(ExecutiveMetrics.IsOverdue(TaskStatuses.InProgress, today.AddDays(-1), today));
        Assert.False(ExecutiveMetrics.IsOverdue(TaskStatuses.Completed, today.AddDays(-1), today));
        Assert.True(ExecutiveMetrics.ProjectAtRisk(ProjectStatuses.Active, today.AddDays(-1), today, false, false));
        Assert.True(ExecutiveMetrics.ProjectAtRisk(ProjectStatuses.OnHold, today.AddDays(3), today, true, false));
        Assert.False(ExecutiveMetrics.ProjectAtRisk(ProjectStatuses.Completed, today.AddDays(-1), today, true, true));
        Assert.False(ExecutiveMetrics.ProjectAtRisk(ProjectStatuses.Active, today.AddDays(3), today, false, false));
        Assert.True(ExecutiveMetrics.IsStale(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc), 14));
        Assert.False(ExecutiveMetrics.IsStale(new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc), 14));
    }

    [Fact]
    public void A_department_with_active_people_or_projects_cannot_be_deactivated()
    {
        Assert.True(DepartmentRules.CanDeactivateDepartment(0, 0));
        Assert.False(DepartmentRules.CanDeactivateDepartment(1, 0));
        Assert.False(DepartmentRules.CanDeactivateDepartment(0, 2));
    }
}
