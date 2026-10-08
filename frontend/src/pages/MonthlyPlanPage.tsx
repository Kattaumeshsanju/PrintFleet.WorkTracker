import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { TaskModal } from "../components/TaskModal";
import { Badge, Button, Loading, PageHeader, useToast } from "../components/ui";
import { api, errorText } from "../services/api";
import type { MonthlySheet, MonthlyTask, Project, User } from "../types";
import { currentMonthValue, formatHours, label, monthLabel, parseMonth, shortDate } from "../utils/format";

export function MonthlyPlanPage() {
  const { user } = useAuth();
  const toast = useToast();
  const [params] = useSearchParams();
  const requestedUser = params.get("userId") ?? "";
  const [month, setMonth] = useState(currentMonthValue());
  const [sheet, setSheet] = useState<MonthlySheet | null>(null);
  const [projects, setProjects] = useState<Project[]>([]);
  const [people, setPeople] = useState<User[]>([]);
  const [editing, setEditing] = useState<MonthlyTask | null>(null);
  const [open, setOpen] = useState(false);
  const [error, setError] = useState("");
  const lead = user?.role === "TeamLead";
  const locked = sheet?.status === "Locked";
  const own = !requestedUser || requestedUser === user?.id;
  const canEdit = !!sheet && !locked;

  async function load() {
    const { year, month: monthNumber } = parseMonth(month);
    const plan = await api<MonthlySheet>("/api/monthly-work", {
      method: "POST",
      body: JSON.stringify({ year, month: monthNumber, userId: requestedUser || null }),
    });
    setSheet(plan);
  }

  useEffect(() => {
    api<Project[]>("/api/projects").then(setProjects).catch(() => undefined);
    if (lead) api<User[]>("/api/users").then(setPeople).catch(() => undefined);
  }, [lead]);

  useEffect(() => {
    load().catch((err) => setError(errorText(err)));
  }, [month, requestedUser]);

  async function remove(task: MonthlyTask) {
    if (!window.confirm(`Delete "${task.title}"?`)) return;
    try {
      await api(`/api/monthly-tasks/${task.id}`, { method: "DELETE" });
      toast("Task deleted.");
      await load();
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  async function setLock(lock: boolean) {
    if (!sheet) return;
    try {
      setSheet(await api<MonthlySheet>(`/api/monthly-work/${sheet.id}/${lock ? "lock" : "unlock"}`, { method: "POST" }));
      toast(lock ? "Month locked." : "Month unlocked.");
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  async function changeStatus(task: MonthlyTask, status: string) {
    try {
      await api(`/api/monthly-tasks/${task.id}`, {
        method: "PUT",
        body: JSON.stringify({
          projectId: task.projectId,
          title: task.title,
          description: task.description,
          priority: task.priority,
          plannedStartDate: task.plannedStartDate,
          plannedEndDate: task.plannedEndDate,
          estimatedHours: task.estimatedHours,
          status,
          remarks: task.remarks,
        }),
      });
      await load();
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  if (error) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;
  if (!sheet) return <Loading />;
  const viewing = people.find((person) => person.id === requestedUser);

  return (
    <div>
      <PageHeader
        title={own ? "My Monthly Plan" : `${viewing?.name ?? sheet.userName}'s plan`}
        subtitle={`${monthLabel(sheet.year, sheet.month)} · ${sheet.completionPercent}% complete`}
        actions={<>
          {canEdit ? <Button onClick={() => { setEditing(null); setOpen(true); }}>Add task</Button> : null}
          {lead ? <Button variant="ghost" onClick={() => setLock(!locked)}>{locked ? "Unlock month" : "Lock month"}</Button> : null}
        </>}
      />
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <input className="rounded-xl border border-line bg-white px-3 py-2" type="month" value={month} onChange={(event) => setMonth(event.target.value)} />
        <Badge value={sheet.status} />
        <span className="text-sm text-muted">Planned {formatHours(sheet.plannedHours)} · Actual {formatHours(sheet.actualHours)}</span>
      </div>
      {locked ? <div className="mb-4 rounded-xl bg-stone-100 px-3 py-2 text-sm">This month is locked. Historical records stay unchanged until the Team Lead unlocks the sheet.</div> : null}
      <div className="overflow-x-auto rounded-2xl border border-line bg-card">
        <table className="w-full min-w-[980px] text-left text-sm">
          <thead className="bg-[#f7f3ea] text-xs tracking-wide text-muted uppercase">
            <tr>{["Task", "Project", "Priority", "Planned start", "Planned end", "Est. hours", "Actual", "Status", "Remarks", ""].map((heading) => <th key={heading} className="px-3 py-2">{heading}</th>)}</tr>
          </thead>
          <tbody>
            {sheet.tasks.length === 0 ? <tr><td className="px-3 py-8 text-muted" colSpan={10}>No planned tasks for this month.</td></tr> : null}
            {sheet.tasks.map((task) => (
              <tr key={task.id} className="border-t border-line align-top">
                <td className="px-3 py-2 font-semibold">{task.title}{task.overdue ? <div className="text-xs text-rose-700">Overdue</div> : null}<div className="font-normal text-muted">{task.description}</div></td>
                <td>{task.projectName ?? "—"}</td>
                <td><Badge value={task.priority} /></td>
                <td>{shortDate(task.plannedStartDate)}</td>
                <td>{shortDate(task.plannedEndDate)}</td>
                <td className="font-mono">{task.estimatedHours}</td>
                <td className="font-mono">{task.actualHours}</td>
                <td>
                  {canEdit ? (
                    <select className="rounded-lg border border-line bg-white px-2 py-1" value={task.status} onChange={(event) => changeStatus(task, event.target.value)}>
                      {["Planned", "InProgress", "Completed", "Blocked", "Cancelled"].map((status) => <option key={status} value={status}>{label(status)}</option>)}
                    </select>
                  ) : <Badge value={task.status} />}
                </td>
                <td className="max-w-48 text-muted">{task.remarks}</td>
                <td className="px-3 whitespace-nowrap">
                  {canEdit ? <>
                    <button className="mr-3 font-semibold text-pine" onClick={() => { setEditing(task); setOpen(true); }}>Edit</button>
                    <button className="font-semibold text-rose-700" onClick={() => remove(task)}>Delete</button>
                  </> : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <TaskModal open={open} sheetId={sheet.id} month={month} projects={projects} editing={editing} onClose={() => setOpen(false)} onSaved={() => { load().catch((err) => toast(errorText(err), "error")); }} />
    </div>
  );
}
