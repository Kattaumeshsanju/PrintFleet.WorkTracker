using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public static class PlanReminderMail
{
    public sealed record Built(string Subject, string Text, string Html);

    public static Built Personal(string name, string department, string lead, DateOnly month, IReadOnlyList<PlanGoalLine> goals, string planLink, DateOnly today)
    {
        var active = goals.Where(goal => PlanReminderRules.CountsAsActive(goal.Status)).ToList();
        var cancelled = goals.Where(goal => goal.Status == TaskStatuses.Cancelled).ToList();
        var subject = PlanReminderRules.CleanSubject($"Your {month:MMMM yyyy} plan — {department}");
        var text = new List<string>
        {
            $"Hello {name},",
            "",
            $"Department: {department}",
            $"Team lead: {lead}",
            $"Reporting month: {month:MMMM yyyy}",
            ""
        };
        text.Add(active.Count == 0 ? "You have no active goals this month." : "Your monthly goals:");
        text.AddRange(active.Select(goal => FormatLine(goal, today)));
        if (cancelled.Count > 0)
        {
            text.Add("");
            text.Add("Cancelled goals are not counted in the plan:");
            text.AddRange(cancelled.Select(goal => FormatLine(goal, today)));
        }
        text.Add("");
        text.Add(string.IsNullOrWhiteSpace(planLink) ? "Open your monthly plan in WorkTracker." : $"Open your monthly plan: {planLink}");
        text.Add("");
        text.Add("This is an automated WorkTracker reminder.");
        return new Built(subject, string.Join(Environment.NewLine, text), Html(subject, name, department, lead, month, active, cancelled, [], planLink, "Open your monthly plan", today, false));
    }

    public static Built Missing(string name, string department, string lead, DateOnly month, string planLink)
    {
        var subject = PlanReminderRules.CleanSubject($"Your {month:MMMM yyyy} plan is missing — {department}");
        var text = string.Join(Environment.NewLine, [
            $"Hello {name},",
            "",
            $"Department: {department}",
            $"Team lead: {lead}",
            $"Reporting month: {month:MMMM yyyy}",
            "",
            "WorkTracker does not have a monthly plan for you for this month.",
            string.IsNullOrWhiteSpace(planLink) ? "Add your goals in WorkTracker." : $"Add your goals: {planLink}",
            "",
            "This is an automated WorkTracker reminder."
        ]);
        var body = $"""
            <p style="margin:0 0 12px;">Hello {PlanReminderRules.Encode(name)},</p>
            <p style="margin:0 0 12px;">There is no monthly plan saved for you for {PlanReminderRules.Encode(month.ToString("MMMM yyyy"))}.</p>
            {Button(planLink, "Add your monthly plan")}
            """;
        return new Built(subject, text, Shell(subject, department, lead, month, body));
    }

    public static Built Summary(string leadName, string department, DateOnly month, int members, int planned, int completed, int inProgress, int overdue, int blocked, int missing, IReadOnlyList<(string Who, PlanGoalLine Goal)> upcoming, string overviewLink, DateOnly today)
    {
        var subject = PlanReminderRules.CleanSubject($"{department} plan summary — {month:MMMM yyyy}");
        var text = string.Join(Environment.NewLine, [
            $"Hello {leadName},",
            "",
            $"Department summary for {department}, {month:MMMM yyyy}.",
            $"Active members: {members}",
            $"Goals counted: {planned}",
            $"Completed: {completed}",
            $"In progress: {inProgress}",
            $"Overdue: {overdue}",
            $"Blocked: {blocked}",
            $"Missing plans: {missing}",
            "",
            string.IsNullOrWhiteSpace(overviewLink) ? "Open the department monthly plan in WorkTracker." : $"Department overview: {overviewLink}",
            "",
            "This summary does not include anyone's private daily timeline.",
            "This is an automated WorkTracker reminder."
        ]);
        var stats = $"""
            <p style="margin:0 0 12px;">Hello {PlanReminderRules.Encode(leadName)}, this is the department summary. It does not include private daily timelines.</p>
            <table role="presentation" width="100%" cellpadding="8" cellspacing="0" style="border-collapse:collapse;margin:0 0 16px;">
              <tr>
                {Stat("Members", members)}{Stat("Counted", planned)}{Stat("Completed", completed)}{Stat("In progress", inProgress)}{Stat("Overdue", overdue)}{Stat("Blocked", blocked)}{Stat("Missing plans", missing)}
              </tr>
            </table>
            """;
        var upcomingHtml = upcoming.Count == 0
            ? "<p style=\"margin:0 0 12px;\">No deadlines in the next 7 days.</p>"
            : Table(upcoming.Select(item => item.Goal with { Title = $"{item.Who}: {item.Goal.Title}" }).ToList(), today);
        return new Built(subject, text, Shell(subject, department, leadName, month, stats + upcomingHtml + Button(overviewLink, "Open the department plan")));
    }

    private static string FormatLine(PlanGoalLine goal, DateOnly today)
    {
        var mark = PlanReminderRules.IsOverdue(goal, today) ? "Overdue" : Label(goal.Status);
        var project = string.IsNullOrWhiteSpace(goal.Project) ? "" : $" ({goal.Project})";
        return $"- {goal.Title}{project} — {mark} — {goal.ProgressPercent}% — due {goal.End:dd MMM yyyy}";
    }

    private static string Html(string subject, string name, string department, string lead, DateOnly month, IReadOnlyList<PlanGoalLine> active, IReadOnlyList<PlanGoalLine> cancelled, IReadOnlyList<PlanGoalLine> unused, string link, string linkLabel, DateOnly today, bool unusedFlag)
    {
        _ = unused;
        _ = unusedFlag;
        var intro = $"<p style=\"margin:0 0 12px;\">Hello {PlanReminderRules.Encode(name)}, here is your own monthly plan. Other people's plans and daily timelines are not included.</p>";
        var activeHtml = active.Count == 0 ? "<p style=\"margin:0 0 12px;\">No active goals are saved for this month.</p>" : Table(active, today);
        var cancelledHtml = cancelled.Count == 0 ? "" : $"<p style=\"margin:16px 0 8px;font-weight:bold;\">Cancelled, not counted</p>{Table(cancelled, today)}";
        return Shell(subject, department, lead, month, intro + activeHtml + cancelledHtml + Button(link, linkLabel));
    }

    private static string Shell(string title, string department, string lead, DateOnly month, string body)
    {
        return $"""
            <!DOCTYPE html>
            <html><body style="margin:0;padding:0;background:#f4f6f8;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f4f6f8;padding:24px 12px;">
              <tr><td align="center">
                <table role="presentation" width="640" cellpadding="0" cellspacing="0" style="width:640px;max-width:100%;background:#ffffff;border:1px solid #e4e7ec;">
                  <tr><td style="background:#101820;color:#ffffff;padding:20px 24px;font-family:Arial,sans-serif;">
                    <div style="font-size:12px;letter-spacing:1px;color:#a9782a;">WORKTRACKER</div>
                    <div style="font-size:22px;font-weight:bold;margin-top:4px;">{PlanReminderRules.Encode(title)}</div>
                  </td></tr>
                  <tr><td style="padding:20px 24px;font-family:Arial,sans-serif;font-size:14px;color:#1c2430;line-height:1.45;">
                    <p style="margin:0 0 12px;">Department: <strong>{PlanReminderRules.Encode(department)}</strong><br>Team lead: <strong>{PlanReminderRules.Encode(lead)}</strong><br>Reporting month: <strong>{PlanReminderRules.Encode(month.ToString("MMMM yyyy"))}</strong></p>
                    {body}
                    <p style="margin:20px 0 0;color:#5c6570;font-size:12px;">This is an automated WorkTracker reminder sent from the organization's mailbox. Daily work descriptions are not included.</p>
                  </td></tr>
                </table>
              </td></tr>
            </table>
            </body></html>
            """;
    }

    private static string Table(IReadOnlyList<PlanGoalLine> goals, DateOnly today)
    {
        var rows = string.Join("", goals.Select(goal =>
        {
            var overdue = PlanReminderRules.IsOverdue(goal, today);
            var label = overdue ? "Overdue" : Label(goal.Status);
            var color = Color(goal.Status, overdue);
            var detail = string.IsNullOrWhiteSpace(goal.Description) ? "" : $"<div style=\"color:#5c6570;margin-top:4px;\">{PlanReminderRules.Encode(goal.Description)}</div>";
            var blocker = goal.Status == TaskStatuses.Blocked && !string.IsNullOrWhiteSpace(goal.Remarks)
                ? $"<div style=\"color:#9f1239;margin-top:4px;\">Blocker: {PlanReminderRules.Encode(goal.Remarks)}</div>"
                : "";
            return $"""
                <tr>
                  <td style="border-bottom:1px solid #e4e7ec;padding:8px;font-family:Arial,sans-serif;font-size:13px;">{PlanReminderRules.Encode(goal.Title)}{detail}{blocker}<div style="color:#5c6570;margin-top:4px;">{PlanReminderRules.Encode(goal.Project ?? "No project")} · {goal.Start:dd MMM}–{goal.End:dd MMM}</div></td>
                  <td style="border-bottom:1px solid #e4e7ec;padding:8px;font-family:Arial,sans-serif;font-size:13px;color:{color};font-weight:bold;">{label}</td>
                  <td style="border-bottom:1px solid #e4e7ec;padding:8px;font-family:Arial,sans-serif;font-size:13px;">{Math.Clamp(goal.ProgressPercent, 0, 100)}%</td>
                </tr>
                """;
        }));
        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;">
              <tr>
                <th align="left" style="border-bottom:1px solid #e4e7ec;padding:8px;font-family:Arial,sans-serif;font-size:12px;color:#5c6570;">Goal</th>
                <th align="left" style="border-bottom:1px solid #e4e7ec;padding:8px;font-family:Arial,sans-serif;font-size:12px;color:#5c6570;">Status</th>
                <th align="left" style="border-bottom:1px solid #e4e7ec;padding:8px;font-family:Arial,sans-serif;font-size:12px;color:#5c6570;">Progress</th>
              </tr>
              {rows}
            </table>
            """;
    }

    private static string Stat(string label, int value)
        => $"<td style=\"font-family:Arial,sans-serif;font-size:12px;color:#5c6570;\">{PlanReminderRules.Encode(label)}<div style=\"font-size:18px;color:#1c2430;font-weight:bold;\">{value}</div></td>";

    private static string Button(string link, string label)
    {
        if (string.IsNullOrWhiteSpace(link)) return "";
        return $"<p style=\"margin:16px 0 0;\"><a href=\"{PlanReminderRules.Encode(link)}\" style=\"background:#a9782a;color:#ffffff;text-decoration:none;padding:10px 16px;display:inline-block;font-family:Arial,sans-serif;\">{PlanReminderRules.Encode(label)}</a></p>";
    }

    private static string Label(string status) => status switch
    {
        TaskStatuses.InProgress => "In progress",
        TaskStatuses.Completed => "Completed",
        TaskStatuses.Blocked => "Blocked",
        TaskStatuses.Cancelled => "Cancelled",
        _ => "Planned"
    };

    private static string Color(string status, bool overdue)
    {
        if (overdue) return "#9a3412";
        return status switch
        {
            TaskStatuses.InProgress => "#a9782a",
            TaskStatuses.Completed => "#166534",
            TaskStatuses.Blocked => "#9f1239",
            TaskStatuses.Cancelled => "#64748b",
            _ => "#1a4f7a"
        };
    }
}
