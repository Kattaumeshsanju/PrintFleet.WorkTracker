using PrintFleet.WorkTracker.Api.Models;
using PrintFleet.WorkTracker.Api.Services;
using Xunit;

namespace PrintFleet.WorkTracker.Api.Tests;

public class DailyReminderRulesTests
{
    [Fact]
    public void Active_members_of_enabled_departments_are_included_and_finished_days_are_not()
    {
        DailyReminderCandidate[] people =
        [
            new(Guid.NewGuid(), "sanju.k@intechadditive.com", Roles.TeamLead, true, true, true),
            new(Guid.NewGuid(), "manjusha.d@intechadditive.com", Roles.TeamMember, true, true, true),
            new(Guid.NewGuid(), "shilpesh.r@intechadditive.com", Roles.TeamLead, true, true, true),
            new(Guid.NewGuid(), "saipavan.k@intechadditive.com", Roles.TeamMember, true, true, false),
            new(Guid.NewGuid(), "admin@intechadditive.com", Roles.Admin, true, true, true),
            new(Guid.NewGuid(), "away@intechadditive.com", Roles.TeamMember, true, true, true),
            new(Guid.NewGuid(), "done@intechadditive.com", Roles.TeamMember, true, true, true),
            new(Guid.NewGuid(), "old@intechadditive.com", Roles.TeamMember, false, true, true),
            new(Guid.NewGuid(), "not-an-email", Roles.TeamMember, true, true, true)
        ];
        var submitted = people[6].UserId;
        var leave = people[5].UserId;
        var selected = DailyReminderRules.Select(
            people,
            id => id == submitted ? SubmissionStatuses.Submitted : SubmissionStatuses.Pending,
            id => id == leave ? DayTypes.Leave : null);

        Assert.Equal(
            ["manjusha.d@intechadditive.com", "sanju.k@intechadditive.com", "shilpesh.r@intechadditive.com"],
            selected.Select(person => person.Email).ToArray());
    }

    [Fact]
    public void Holiday_and_leave_are_both_skipped()
    {
        var person = new DailyReminderCandidate(Guid.NewGuid(), "sanju.k@intechadditive.com", Roles.TeamMember, true, true, true);
        Assert.False(DailyReminderRules.ShouldReceive(person, SubmissionStatuses.Pending, DayTypes.Holiday));
        Assert.False(DailyReminderRules.ShouldReceive(person, SubmissionStatuses.Pending, DayTypes.Leave));
        Assert.True(DailyReminderRules.ShouldReceive(person, SubmissionStatuses.Pending, null));
    }

    [Fact]
    public void Plan_mail_uses_smtp_only_when_graph_is_not_configured()
    {
        Assert.Equal("Graph", PlanReminderRules.MailTransport(true, true));
        Assert.Equal("Smtp", PlanReminderRules.MailTransport(false, true));
        Assert.Null(PlanReminderRules.MailTransport(false, false));
        Assert.Equal("Accepted by SMTP", PlanReminderRules.AcceptanceLabel("Smtp"));
        Assert.Equal("Accepted by Microsoft Graph", PlanReminderRules.AcceptanceLabel("Graph"));
    }
}
