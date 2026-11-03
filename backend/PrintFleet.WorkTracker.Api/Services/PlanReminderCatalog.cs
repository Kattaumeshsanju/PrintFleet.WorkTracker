using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public sealed record PlanOutgoing(Guid UserId, Guid DepartmentId, string DepartmentName, string Email, string MessageType, PlanReminderMail.Built Message);

public class PlanReminderCatalog
{
    private readonly AppDbContext _db;

    public PlanReminderCatalog(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PlanOutgoing>> ComposeAsync(DateOnly today, bool missingPlans, bool summaries, string siteUrl, CancellationToken ct)
    {
        var site = string.IsNullOrWhiteSpace(siteUrl) ? "" : siteUrl.TrimEnd('/');
        var month = new DateOnly(today.Year, today.Month, 1);
        var members = await _db.Users.AsNoTracking()
            .Where(user => user.DepartmentId != null)
            .Select(user => new PlanMember(user.Id, user.Name, user.Email, user.Role, user.DepartmentId!.Value, user.Department!.Name, user.IsActive, user.Department.IsActive))
            .ToListAsync(ct);
        var recipients = PlanReminderRules.PersonalRecipients(members);
        var ids = recipients.Select(person => person.UserId).ToArray();
        var goalRows = await _db.MonthlyTasks.AsNoTracking()
            .Where(task => ids.Contains(task.Sheet.UserId) && task.Sheet.Year == month.Year && task.Sheet.Month == month.Month)
            .Select(task => new
            {
                task.Sheet.UserId,
                task.Title,
                task.Description,
                Project = task.Project != null ? task.Project.Name : null,
                task.Status,
                task.ProgressPercent,
                task.PlannedStartDate,
                task.PlannedEndDate,
                task.Remarks,
                task.EstimatedHours
            })
            .ToListAsync(ct);
        var goalsByUser = goalRows.GroupBy(goal => goal.UserId).ToDictionary(
            group => group.Key,
            group => (IReadOnlyList<PlanGoalLine>)group.Select(goal => new PlanGoalLine(goal.Title, goal.Description, goal.Project, goal.Status, goal.ProgressPercent, goal.PlannedStartDate, goal.PlannedEndDate, goal.Remarks, goal.EstimatedHours)).ToList());
        var sheetIds = await _db.MonthlyWorkSheets.AsNoTracking()
            .Where(sheet => ids.Contains(sheet.UserId) && sheet.Year == month.Year && sheet.Month == month.Month)
            .Select(sheet => sheet.UserId)
            .ToListAsync(ct);
        var hasSheet = sheetIds.ToHashSet();
        var planLink = site.Length == 0 ? "" : $"{site}/monthly-plan";
        var overviewLink = site.Length == 0 ? "" : $"{site}/plans";
        var outgoing = new List<PlanOutgoing>();

        foreach (var person in recipients)
        {
            var own = goalsByUser.GetValueOrDefault(person.UserId) ?? [];
            var lead = PlanReminderRules.LeadName(members.Where(member => member.DepartmentId == person.DepartmentId));
            if (!hasSheet.Contains(person.UserId))
            {
                if (missingPlans)
                    outgoing.Add(new PlanOutgoing(person.UserId, person.DepartmentId, person.DepartmentName, person.Email.Trim(), PlanReminderTypes.MissingPlan, PlanReminderMail.Missing(person.Name, person.DepartmentName, lead, month, planLink)));
                continue;
            }
            outgoing.Add(new PlanOutgoing(person.UserId, person.DepartmentId, person.DepartmentName, person.Email.Trim(), PlanReminderTypes.PersonalPlan, PlanReminderMail.Personal(person.Name, person.DepartmentName, lead, month, own, planLink, today)));
        }

        if (!summaries) return outgoing;
        foreach (var lead in PlanReminderRules.SummaryRecipients(members))
        {
            var departmentPeople = recipients.Where(person => person.DepartmentId == lead.DepartmentId).ToList();
            var departmentGoals = departmentPeople.SelectMany(person => (goalsByUser.GetValueOrDefault(person.UserId) ?? []).Select(goal => (person, goal))).ToList();
            var active = departmentGoals.Where(item => PlanReminderRules.CountsAsActive(item.goal.Status)).Select(item => item.goal).ToList();
            var upcoming = departmentGoals
                .Where(item => PlanReminderRules.CountsAsActive(item.goal.Status) && item.goal.Status != TaskStatuses.Completed && item.goal.End >= today && item.goal.End <= today.AddDays(7))
                .OrderBy(item => item.goal.End)
                .Take(8)
                .Select(item => (item.person.Name, item.goal))
                .ToList();
            outgoing.Add(new PlanOutgoing(
                lead.UserId,
                lead.DepartmentId,
                lead.DepartmentName,
                lead.Email.Trim(),
                PlanReminderTypes.DepartmentSummary,
                PlanReminderMail.Summary(
                    lead.Name,
                    lead.DepartmentName,
                    month,
                    departmentPeople.Count,
                    active.Count,
                    active.Count(goal => goal.Status == TaskStatuses.Completed),
                    active.Count(goal => goal.Status == TaskStatuses.InProgress),
                    active.Count(goal => PlanReminderRules.IsOverdue(goal, today)),
                    active.Count(goal => goal.Status == TaskStatuses.Blocked),
                    departmentPeople.Count(person => !hasSheet.Contains(person.UserId)),
                    upcoming,
                    overviewLink,
                    today)));
        }
        return outgoing;
    }
}
