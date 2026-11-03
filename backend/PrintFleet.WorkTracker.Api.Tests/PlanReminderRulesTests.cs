using PrintFleet.WorkTracker.Api.Models;
using PrintFleet.WorkTracker.Api.Services;
using Xunit;

namespace PrintFleet.WorkTracker.Api.Tests;

public class PlanReminderRulesTests
{
    private static readonly Guid Machine = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid Application = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid Optics = Guid.Parse("20000000-0000-0000-0000-000000000003");
    private static readonly Guid Sanju = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Manjusha = Guid.Parse("10000000-0000-0000-0000-000000000007");
    private static readonly Guid Anvesh = Guid.Parse("10000000-0000-0000-0000-000000000006");
    private static readonly Guid Shilpesh = Guid.Parse("10000000-0000-0000-0000-000000000008");

    [Fact]
    public void Active_members_of_every_department_are_included_without_hardcoded_names()
    {
        var people = new[]
        {
            Member(Sanju, "Sanju", "sanju.k@intechadditive.com", Roles.TeamLead, Machine, "Machine Software"),
            Member(Manjusha, "Manjusha", "manjusha.d@intechadditive.com", Roles.TeamMember, Machine, "Machine Software"),
            Member(Anvesh, "Anvesh", "p.anvesh@intechadditive.com", Roles.TeamMember, Application, "Application"),
            Member(Shilpesh, "Shilpesh", "shilpesh@intechadditive.com", Roles.TeamLead, Optics, "Optics"),
            Member(Guid.NewGuid(), "Admin", "admin@intechadditive.com", Roles.Admin, Machine, "Machine Software"),
            Member(Guid.NewGuid(), "CEO", "ceo@intechadditive.com", Roles.Ceo, Application, "Application"),
            Member(Guid.NewGuid(), "Left", "left@intechadditive.com", Roles.TeamMember, Machine, "Machine Software", userActive: false),
            Member(Guid.NewGuid(), "Closed", "closed@intechadditive.com", Roles.TeamMember, Guid.NewGuid(), "Closed", departmentActive: false),
            Member(Guid.NewGuid(), "Bad", "not an email", Roles.TeamMember, Machine, "Machine Software"),
            Member(Sanju, "Sanju duplicate", "sanju.k@intechadditive.com", Roles.TeamLead, Machine, "Machine Software"),
        };

        var selected = PlanReminderRules.PersonalRecipients(people);
        Assert.Equal(4, selected.Count);
        Assert.Contains(selected, person => person.DepartmentName == "Machine Software" && person.UserId == Sanju);
        Assert.Contains(selected, person => person.DepartmentName == "Application");
        Assert.Contains(selected, person => person.DepartmentName == "Optics");
        Assert.DoesNotContain(selected, person => person.Role is Roles.Admin or Roles.Ceo);
        Assert.Equal(1, selected.Count(person => person.UserId == Sanju));
    }

    [Fact]
    public void Summaries_go_only_to_each_departments_team_lead()
    {
        var people = new[]
        {
            Member(Sanju, "Sanju", "sanju.k@intechadditive.com", Roles.TeamLead, Machine, "Machine Software"),
            Member(Manjusha, "Manjusha", "manjusha.d@intechadditive.com", Roles.TeamMember, Machine, "Machine Software"),
            Member(Shilpesh, "Shilpesh", "shilpesh@intechadditive.com", Roles.TeamLead, Optics, "Optics"),
        };
        var leads = PlanReminderRules.SummaryRecipients(people);
        Assert.Equal(2, leads.Count);
        Assert.All(leads, lead => Assert.Equal(Roles.TeamLead, lead.Role));
    }

    [Fact]
    public void Schedule_stays_at_1700_on_weekdays_in_the_selected_set()
    {
        var monday = new DateTime(2026, 11, 2, 17, 0, 0);
        var before = monday.AddMinutes(-1);
        var saturday = new DateTime(2026, 11, 7, 17, 0, 0);
        Assert.True(PlanReminderRules.IsDue(monday, 17, 0, "1,2,3,4,5"));
        Assert.False(PlanReminderRules.IsDue(before, 17, 0, "1,2,3,4,5"));
        Assert.False(PlanReminderRules.IsDue(saturday, 17, 0, "1,2,3,4,5"));
        Assert.False(PlanReminderRules.IsDue(monday, 17, 0, "2,3,4,5"));
    }

    [Fact]
    public void Accepted_graph_requests_are_not_retried_and_transient_failures_are()
    {
        var now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(PlanReminderRules.MayAttempt(PlanReminderStatuses.Accepted, false, 1, null, now));
        Assert.False(PlanReminderRules.MayAttempt(PlanReminderStatuses.Processing, true, 1, null, now));
        Assert.True(PlanReminderRules.MayAttempt(PlanReminderStatuses.Failed, true, 1, now.AddMinutes(-1), now));
        Assert.False(PlanReminderRules.MayAttempt(PlanReminderStatuses.Failed, false, 1, null, now));
        Assert.False(PlanReminderRules.MayAttempt(PlanReminderStatuses.Failed, true, PlanReminderRules.MaxAttempts, null, now));
    }

    [Fact]
    public void The_same_person_and_day_keep_one_idempotency_key()
    {
        var day = new DateOnly(2026, 11, 3);
        var first = PlanReminderRules.Key(day, PlanReminderTypes.PersonalPlan, Sanju, 2026, 11);
        var second = PlanReminderRules.Key(day, PlanReminderTypes.PersonalPlan, Sanju, 2026, 11);
        var summary = PlanReminderRules.Key(day, PlanReminderTypes.DepartmentSummary, Sanju, 2026, 11);
        Assert.Equal(first, second);
        Assert.NotEqual(first, summary);
    }

    [Fact]
    public void Cancelled_goals_stay_out_of_active_totals_and_html_is_encoded()
    {
        var today = new DateOnly(2026, 11, 3);
        var goals = new[]
        {
            new PlanGoalLine("Ship <script>", "desc", "Project", TaskStatuses.Completed, 100, today, today, null, 10),
            new PlanGoalLine("Dropped", null, null, TaskStatuses.Cancelled, 0, today, today, null, 30),
        };
        var mail = PlanReminderMail.Personal("A&B", "Machine", "Lead", new DateOnly(2026, 11, 1), goals, "http://localhost/monthly-plan", today);
        Assert.Contains("Ship &lt;script&gt;", mail.Html);
        Assert.Contains("A&amp;B", mail.Html);
        Assert.Contains("Cancelled, not counted", mail.Html);
        Assert.DoesNotContain("<script>", mail.Html);
        var counted = goals.Where(goal => PlanReminderRules.CountsAsActive(goal.Status)).ToList();
        Assert.Single(counted);
        Assert.Equal(100, WorkRecords.PlanCompletion(counted.Select(goal => (goal.Status, goal.ProgressPercent, goal.EstimatedHours))));
    }

    [Fact]
    public void Graph_acceptance_is_separate_from_delivery()
    {
        var accepted = new GraphSendResult(true, false, 202, null);
        Assert.True(accepted.Accepted);
        Assert.NotEqual("Delivered", PlanReminderStatuses.Accepted);
        Assert.Equal("Accepted", PlanReminderStatuses.Accepted);
    }

    private static PlanMember Member(Guid id, string name, string email, string role, Guid departmentId, string department, bool userActive = true, bool departmentActive = true)
        => new(id, name, email, role, departmentId, department, userActive, departmentActive);
}
