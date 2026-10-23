import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { WorkEntryModal } from "../components/WorkEntryModal";
import { Badge, Button, Loading, Panel, Stat } from "../components/ui";
import { DayTimeline, EntryList } from "../timeline/DayTimeline";
import { api, errorText } from "../services/api";
import { useToast } from "../components/ui";
import type { MonthlySheet, MyDashboard, TeamDashboard } from "../types";
import { canManage, currentMonthValue, formatMinutes, greeting, longDate, parseMonth } from "../utils/format";

type View = "mine" | "team" | "combined";

export function DashboardPage() {
  const { user } = useAuth();
  const toast = useToast();
  const lead = canManage(user?.role);
  const [mine, setMine] = useState<MyDashboard | null>(null);
  const [team, setTeam] = useState<TeamDashboard | null>(null);
  const [sheet, setSheet] = useState<MonthlySheet | null>(null);
  const [view, setView] = useState<View>("combined");
  const [open, setOpen] = useState(false);
  const [error, setError] = useState("");

  async function load() {
    const month = parseMonth(currentMonthValue());
    const [myData, plan] = await Promise.all([
      api<MyDashboard>("/api/dashboard/my"),
      api<MonthlySheet>("/api/monthly-work", { method: "POST", body: JSON.stringify(month) }),
    ]);
    setMine(myData);
    setSheet(plan);
    if (lead) setTeam(await api<TeamDashboard>("/api/dashboard/team"));
  }

  useEffect(() => {
    load().catch((err) => setError(errorText(err)));
  }, [lead]);

  async function submitDay() {
    if (!mine) return;
    try {
      await api("/api/daily-work/submit", { method: "POST", body: JSON.stringify({ workDate: mine.date }) });
      toast("Daily update submitted.");
      await load();
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  if (error) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;
  if (!mine || !user) return <Loading />;

  const others = team?.members.filter((member) => member.userId !== user.id) ?? [];
  const combined = (team?.members ?? [])
    .flatMap((member) => member.entries.map((entry) => ({ entry, name: member.userId === user.id ? "You" : member.name })))
    .sort((a, b) => a.entry.startTime.localeCompare(b.entry.startTime) || a.name.localeCompare(b.name));

  return (
    <div className="space-y-5">
      <div>
        <h1 className="font-display text-3xl font-semibold">{greeting(user.name)}</h1>
        <p className="text-sm text-muted">{longDate(mine.date)} · {user.jobTitle ?? user.role}</p>
      </div>

      {lead && team ? (
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
          <Stat label="Team members" value={String(team.memberCount)} />
          <Stat label="Updated today" value={`${team.updatedToday} / ${team.memberCount}`} />
          <Stat label="Total hours today" value={formatMinutes(team.totalMinutesToday)} />
          <Stat label="Open tasks" value={String(team.openTasks)} />
          <Stat label="Completed this month" value={String(team.completedThisMonth)} />
          <Stat label="Blocked tasks" value={String(team.blockedTasks)} />
        </div>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <Stat label="Today's hours" value={formatMinutes(mine.todayMinutes)} />
          <Stat label="Tasks completed" value={String(mine.tasksCompleted)} />
          <Stat label="Tasks in progress" value={String(mine.tasksInProgress)} />
          <Stat label="Daily update" value={mine.dailyUpdateStatus === "Submitted" ? "Submitted" : "Pending"} />
        </div>
      )}

      {lead ? (
        <div className="flex gap-2">
          {([["mine", "My Work"], ["team", "Team Work"], ["combined", "Combined View"]] as const).map(([key, text]) => (
            <button key={key} className={`rounded-full px-4 py-1.5 text-sm font-semibold ${view === key ? "bg-ink text-white" : "bg-card text-ink ring-1 ring-line"}`} onClick={() => setView(key)}>{text}</button>
          ))}
        </div>
      ) : null}

      {(view === "mine" || !lead) && (
        <Panel title="What did you work on today?" action={<Button onClick={() => setOpen(true)} disabled={!mine.today.canEdit}>+ Add My Work</Button>}>
          <DayTimeline entries={mine.today.entries} />
          <div className="mt-3"><EntryList entries={mine.today.entries} /></div>
          <div className="mt-3 flex items-center justify-between">
            <div className="font-display text-lg font-semibold">Total today: {formatMinutes(mine.todayMinutes)}</div>
            {mine.today.canEdit ? <Button variant="secondary" onClick={submitDay} disabled={mine.today.entries.length === 0}>Submit Daily Update</Button> : <Badge value={mine.today.submissionStatus} />}
          </div>
        </Panel>
      )}

      {lead && view === "team" && team ? (
        <Panel title="Team activity today">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[720px] text-left text-sm">
              <thead className="text-xs tracking-wide text-muted uppercase"><tr><th className="py-2">Employee</th><th>Current activity</th><th>Start</th><th>End</th><th>Status</th><th>Hours</th><th>Update</th></tr></thead>
              <tbody>
                {others.map((member) => (
                  <tr key={member.userId} className="border-t border-line">
                    <td className="py-2 font-semibold"><Link to={`/team/${member.userId}`}>{member.name}</Link><div className="text-xs text-muted">{member.jobTitle}</div></td>
                    <td>{member.currentActivity ?? "—"}<div className="text-xs text-muted">{member.currentTask}</div></td>
                    <td className="font-mono">{member.currentStart ?? "—"}</td>
                    <td className="font-mono">{member.currentEnd ?? "—"}</td>
                    <td>{member.currentStatus ? <Badge value={member.currentStatus} /> : "—"}</td>
                    <td className="font-mono">{formatMinutes(member.totalMinutes)}</td>
                    <td><Badge value={member.updateStatus} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Panel>
      ) : null}

      {lead && view === "combined" ? (
        <Panel title="Combined timeline">
          <div className="divide-y divide-line">
            {combined.map(({ entry, name }) => (
              <div key={entry.id} className="grid grid-cols-[5.5rem_8rem_1fr] gap-3 py-3 text-sm">
                <div className="font-mono">{entry.startTime}</div>
                <div className="font-semibold">{name}</div>
                <div><span className="font-medium">{entry.monthlyTaskTitle}</span> <span className="text-muted">→ {entry.description}</span></div>
              </div>
            ))}
          </div>
        </Panel>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-2">
        <Panel title="My monthly progress">
          <div className="mb-2 h-2 overflow-hidden rounded-full bg-[#efeadd]">
            <div className="h-full bg-pine" style={{ width: `${Math.min(100, mine.month.completionPercent)}%` }} />
          </div>
          <div className="grid grid-cols-2 gap-2 text-sm">
            <div>Planned tasks <strong>{mine.month.plannedTasks}</strong></div>
            <div>Completed <strong>{mine.month.completed}</strong></div>
            <div>Planned hours <strong>{mine.month.plannedHours}</strong></div>
            <div>Actual hours <strong>{mine.month.actualHours}</strong></div>
          </div>
          <Link className="mt-3 inline-block text-sm font-semibold text-pine" to="/monthly-plan">Open monthly plan</Link>
        </Panel>
        <Panel title="Upcoming tasks">
          {mine.upcomingTasks.length === 0 ? <div className="text-sm text-muted">No open tasks this month.</div> : (
            <div className="space-y-2">
              {mine.upcomingTasks.map((task) => (
                <div key={task.id} className="flex items-center justify-between gap-3 rounded-xl bg-[#f7f3ea] px-3 py-2">
                  <div>
                    <div className="font-semibold">{task.title}</div>
                    <div className="text-xs text-muted">{task.projectName ?? "No project"} · due {task.plannedEndDate}</div>
                  </div>
                  <Badge value={task.status} />
                </div>
              ))}
            </div>
          )}
        </Panel>
      </div>

      <WorkEntryModal open={open} date={mine.date} tasks={sheet?.tasks ?? []} entries={mine.today.entries} editing={null} onClose={() => setOpen(false)} onSaved={() => { load().catch((err) => toast(errorText(err), "error")); }} />
    </div>
  );
}
