using PrintFleet.WorkTracker.Api.Models;
using PrintFleet.WorkTracker.Api.Services;
using Xunit;

namespace PrintFleet.WorkTracker.Api.Tests;

public class TeamNoticeRulesTests
{
    private static readonly Guid Lead = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Member = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid Outsider = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid Machine = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid Application = Guid.Parse("20000000-0000-0000-0000-000000000002");

    [Fact]
    public void A_team_lead_can_add_an_unassigned_employee_but_not_transfer_or_promote()
    {
        Assert.True(TeamNoticeRules.CanAddExisting(Roles.TeamMember, true, null, Machine));
        Assert.True(TeamNoticeRules.CanAddExisting(Roles.TeamMember, true, Machine, Machine));
        Assert.False(TeamNoticeRules.CanAddExisting(Roles.TeamMember, true, Application, Machine));
        Assert.True(TeamNoticeRules.RequiresAdminTransfer(Application, Machine));
        Assert.False(TeamNoticeRules.CanAddExisting(Roles.Admin, true, null, Machine));
        Assert.False(TeamNoticeRules.CanAddExisting(Roles.Ceo, true, null, Machine));
        Assert.False(TeamNoticeRules.CanAddExisting(Roles.TeamMember, false, null, Machine));
    }

    [Fact]
    public void Ordinary_removal_cannot_remove_the_lead_or_the_actor()
    {
        Assert.True(TeamNoticeRules.CanRemoveMember(Lead, Member, Roles.TeamMember));
        Assert.False(TeamNoticeRules.CanRemoveMember(Lead, Lead, Roles.TeamLead));
        Assert.False(TeamNoticeRules.CanRemoveMember(Lead, Member, Roles.TeamLead));
        Assert.False(TeamNoticeRules.CanRemoveMember(Lead, Member, Roles.Admin));
    }

    [Fact]
    public void Recipients_come_from_the_department_and_a_copy_is_not_duplicated()
    {
        var team = new HashSet<Guid> { Lead, Member };
        var selected = TeamNoticeRules.ResolveRecipients(team, [Member], false, true, Lead);
        Assert.Equal([Member, Lead], selected);

        var already = TeamNoticeRules.ResolveRecipients(team, [Lead, Member], false, true, Lead);
        Assert.Equal(2, already.Count);
        Assert.Equal(already.Distinct().Count(), already.Count);

        var everyone = TeamNoticeRules.ResolveRecipients(team, [Outsider], true, true, Lead);
        Assert.Equal(2, everyone.Count);
        Assert.DoesNotContain(Outsider, everyone);

        var denied = Assert.Throws<AppException>(() => TeamNoticeRules.ResolveRecipients(team, [Outsider], false, false, Lead));
        Assert.Equal(403, denied.StatusCode);
    }

    [Fact]
    public void Message_text_drops_markup_and_header_breaks()
    {
        Assert.Equal("Hello team", TeamNoticeRules.Subject("Hello\r\nteam"));
        Assert.DoesNotContain("<script>", TeamNoticeRules.Body("<script>alert(1)</script>\nShip the build"));
        Assert.Contains("Ship the build", TeamNoticeRules.Body("<script>alert(1)</script>\nShip the build"));
        Assert.Equal("Ship&lt;b&gt;it&lt;/b&gt;", TeamNoticeRules.Html("Ship<b>it</b>"));
        Assert.Equal("/projects", TeamNoticeRules.Link(false, Guid.NewGuid(), Machine, Machine));
        Assert.Equal("/plans", TeamNoticeRules.Link(true, null, null, Machine));
        Assert.Throws<AppException>(() => TeamNoticeRules.Link(false, Guid.NewGuid(), Application, Machine));
    }

    [Fact]
    public void Mail_acceptance_is_not_delivery_and_retries_do_not_resend_success()
    {
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        var delivery = new EmailDelivery { Status = EmailDeliveryStatuses.Pending };
        TeamNoticeRules.RecordAttempt(delivery, true, null, false, now);
        Assert.Equal(EmailDeliveryStatuses.SentToProvider, delivery.Status);
        Assert.NotEqual("Delivered", delivery.Status);
        Assert.False(TeamNoticeRules.QueueRetry(delivery, now));

        var failed = new EmailDelivery { Status = EmailDeliveryStatuses.Pending };
        TeamNoticeRules.RecordAttempt(failed, false, "SmtpCommandException", false, now);
        Assert.Equal(EmailDeliveryStatuses.Failed, failed.Status);
        Assert.True(TeamNoticeRules.QueueRetry(failed, now));
        Assert.Equal(EmailDeliveryStatuses.Pending, failed.Status);

        var permanent = new EmailDelivery { Status = EmailDeliveryStatuses.Pending };
        TeamNoticeRules.RecordAttempt(permanent, false, "InvalidAddress", true, now);
        Assert.False(TeamNoticeRules.CanRetry(permanent.Status, permanent.Attempts, permanent.ErrorType));
        Assert.False(TeamNoticeRules.QueueRetry(permanent, now));

        Assert.Equal(EmailDeliveryStatuses.SentToProvider, TeamNoticeRules.Overall([EmailDeliveryStatuses.SentToProvider], true));
        Assert.Equal("Partial", TeamNoticeRules.Overall([EmailDeliveryStatuses.SentToProvider, EmailDeliveryStatuses.Failed], true));
        Assert.DoesNotContain("Delivered", new[] { TeamNoticeRules.Overall([EmailDeliveryStatuses.SentToProvider], true) });
    }

    [Fact]
    public void Invitation_tokens_are_stored_only_as_a_hash()
    {
        var token = TeamNoticeRules.NewToken();
        var hash = TeamNoticeRules.HashToken(token);
        Assert.NotEqual(token, hash);
        Assert.Equal(hash, TeamNoticeRules.HashToken(token));
        Assert.Equal(64, hash.Length);
    }
}
