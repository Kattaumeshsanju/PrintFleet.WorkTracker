using PrintFleet.WorkTracker.Api.Models;
using PrintFleet.WorkTracker.Api.Services;
using Xunit;

namespace PrintFleet.WorkTracker.Api.Tests;

public class CategoryRulesTests
{
    private static readonly Guid Machine = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid Application = Guid.Parse("20000000-0000-0000-0000-000000000002");

    [Fact]
    public void A_new_department_has_no_implied_categories_and_names_stay_local()
    {
        Assert.Equal("future work", CategoryRules.Key("  Future   work "));
        Assert.Equal(CategoryRules.Key("Optics"), CategoryRules.Key(" optics "));
        Assert.NotEqual(Machine, Application);
        Assert.True(CategoryRules.CanManage(Roles.Admin, null, Application));
        Assert.True(CategoryRules.CanManage(Roles.TeamLead, Machine, Machine));
        Assert.False(CategoryRules.CanManage(Roles.TeamLead, Machine, Application));
        Assert.False(CategoryRules.CanManage(Roles.TeamMember, Machine, Machine));
        Assert.False(CategoryRules.CanManage(Roles.Ceo, null, Machine));
    }

    [Fact]
    public void Inactive_categories_remain_on_old_records_but_cannot_be_chosen_for_new_work()
    {
        Assert.True(CategoryRules.CanAssign(Machine, Machine, true, false));
        Assert.True(CategoryRules.CanAssign(Machine, Machine, false, true));
        Assert.False(CategoryRules.CanAssign(Machine, Machine, false, false));
        Assert.False(CategoryRules.CanAssign(Application, Machine, true, false));
        Assert.True(CategoryRules.CanReassign(Machine, Machine));
        Assert.False(CategoryRules.CanReassign(Machine, Application));
    }

    [Fact]
    public void Research_stage_is_independent_of_status_and_progress_is_not_an_average()
    {
        Assert.Contains(InitiativeStatuses.Ongoing, InitiativeStatuses.All);
        Assert.Contains(ResearchStages.ProofOfConcept, ResearchStages.All);
        Assert.DoesNotContain(ResearchStages.ProofOfConcept, InitiativeStatuses.All);
        Assert.Null(CategoryRules.Completion(0, 0));
        Assert.Equal(50m, CategoryRules.Completion(1, 2));
        Assert.Equal("RD-003", CategoryRules.NextNumber(["RD-001", "RD-002", "OTHER"]));
    }

    [Fact]
    public void A_goal_uses_the_initiative_link_instead_of_creating_another_project()
    {
        var project = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var category = Guid.Parse("30000000-0000-0000-0000-000000000002");
        var linked = CategoryRules.LinkGoal(null, project, null, category);
        Assert.Equal(project, linked.ProjectId);
        Assert.Equal(category, linked.CategoryId);
        Assert.Throws<AppException>(() => CategoryRules.LinkGoal(Guid.NewGuid(), project, null, null));
        Assert.False(CategoryRules.CanEditInitiative(Roles.Ceo, null, Machine));
        Assert.False(CategoryRules.CanAddResearch(Roles.TeamMember, Machine, Application, true));
        Assert.True(CategoryRules.CanAddResearch(Roles.TeamMember, Machine, Machine, true));
    }
}
