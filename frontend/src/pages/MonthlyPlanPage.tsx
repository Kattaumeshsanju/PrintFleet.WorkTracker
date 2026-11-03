import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { TaskModal } from "../components/TaskModal";
import { Badge, Button, Loading, PageHeader, useToast } from "../components/ui";
import { api, errorText } from "../services/api";
import type { MonthlySheet, MonthlyTask, Project, User, WorkCategory } from "../types";
import { canManage, currentMonthValue, formatHours, goalCompletion, label, monthLabel, parseMonth, shortDate } from "../utils/format";

export function MonthlyPlanPage() {
  const { user } = useAuth();
  const toast = useToast();
  const [params] = useSearchParams();
  const requestedUser = params.get("userId") ?? "";
  const [month, setMonth] = useState(currentMonthValue());
  const [sheet, setSheet] = useState<MonthlySheet | null>(null);
  const [projects, setProjects] = useState<Project[]>([]);
  const [categories, setCategories] = useState<WorkCategory[]>([]);
  const [initiatives, setInitiatives] = useState<{ id: string; number: string; title: string }[]>([]);
  const [people, setPeople] = useState<User[]>([]);
  const [editing, setEditing] = useState<MonthlyTask | null>(null);
  const [open, setOpen] = useState(false);
  const [error, setError] = useState("");
  const lead = canManage(user?.role);
  const locked = sheet?.status === "Locked";
  const own = !requestedUser || requestedUser === user?.id;
  const canEdit = !!sheet && !locked && (own || lead);

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
    api<WorkCategory[]>("/api/work-categories").then(setCategories).catch(() => undefined);
    api<{ items: { id: string; number: string; title: string }[] }>("/api/initiatives?pageSize=100").then((page) => setInitiatives(page.items)).catch(() => undefined);
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

  async function saveTask(task: MonthlyTask, status: string, progressPercent: number) {
    await api(`/api/monthly-tasks/${task.id}`, {
      method: "PUT",
      body: JSON.stringify({
        projectId: task.projectId,
        workCategoryId: task.workCategoryId,
        initiativeId: task.initiativeId,
        title: task.title,
        description: task.description,
        priority: task.priority,
        plannedStartDate: task.plannedStartDate,
        plannedEndDate: task.plannedEndDate,
        estimatedHours: task.estimatedHours,
        progressPercent,
        status,
        remarks: task.remarks,
      }),
    });
    await load();
  }

  async function changeStatus(task: MonthlyTask, status: string) {
    try {
      await saveTask(task, status, status === "Completed" ? 100 : task.progressPercent);
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  async function changeProgress(task: MonthlyTask, value: string) {
    const progressPercent = Math.min(100, Math.max(0, Math.round(Number(value))));
    if (!Number.isFinite(progressPercent) || progressPercent === task.progressPercent) return;
    try {
      await saveTask(task, task.status, progressPercent);
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
        subtitle={`${sheet.departmentName ? `${sheet.departmentName} · ` : ""}${monthLabel(sheet.year, sheet.month)} · planned work for the month, not today's timeline`}
        actions={<>
          {canEdit ? <Button onClick={() => { setEditing(null); setOpen(true); }}>Add task</Button> : null}
          {lead ? <Button variant="ghost" onClick={() => setLock(!locked)}>{locked ? "Unlock month" : "Lock month"}</Button> : null}
        </>}
      />
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <input className="rounded-xl border border-line bg-white px-3 py-2" type="month" value={month} onChange={(event) => setMonth(event.target.value)} />
        <Badge value={sheet.status} />
        <span className="text-sm text-muted" title="Plan completion weights each goal's done percentage by its estimated hours. Cancelled goals are left out. Logging time does not change it.">Plan {goalCompletion(sheet.completionPercent, sheet.plannedTasks)} · Planned {formatHours(sheet.plannedHours)} · Logged {formatHours(sheet.actualHours)}</span>
      </div>
      {locked ? <div className="mb-4 rounded-xl bg-stone-100 px-3 py-2 text-sm">This month is locked. Historical records stay unchanged until the Team Lead unlocks the sheet.</div> : null}
      <div className="overflow-x-auto rounded-2xl border border-line bg-card">
        <table className="w-full min-w-[980px] text-left text-sm">
          <thead className="bg-slate-50 text-xs tracking-wide text-muted uppercase">
            <tr>{["Task", "Project", "Priority", "Planned start", "Planned end", "Est. hours", "Actual", "Done %", "Status", "Remarks", ""].map((heading) => <th key={heading} className="px-3 py-2">{heading}</th>)}</tr>
          </thead>
          <tbody>
            {sheet.tasks.length === 0 ? <tr><td className="px-3 py-8 text-muted" colSpan={11}>No planned tasks for this month.</td></tr> : null}
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
                  {canEdit && task.status !== "Completed" ? (
                    <input
                      key={`${task.id}-${task.progressPercent}`}
                      className="w-16 rounded-lg border border-line bg-white px-2 py-1 font-mono"
                      type="number"
                      min={0}
                      max={100}
                      defaultValue={task.progressPercent}
                      aria-label={`Done percent for ${task.title}`}
                      onBlur={(event) => changeProgress(task, event.target.value)}
                    />
                  ) : <span className="font-mono">{task.status === "Cancelled" ? "—" : `${task.progressPercent}%`}</span>}
                </td>
                <td>
                  {canEdit ? (
                    <select className="rounded-lg border border-line bg-white px-2 py-1" value={task.status} onChange={(event) => changeStatus(task, event.target.value)}>
                      {["Planned", "InProgress", "Completed", "Blocked", "Cancelled"].map((status) => <option key={status} value={status}>{label(status)}</option>)}
                    </select>
                  ) : <Badge value={task.status} />}
                  {task.actualEndDate ? <div className="mt-1 text-xs text-muted">Finished {shortDate(task.actualEndDate)}</div> : null}
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
      <TaskModal open={open} sheetId={sheet.id} month={month} projects={projects} categories={categories} initiatives={initiatives} editing={editing} onClose={() => setOpen(false)} onSaved={() => { load().catch((err) => toast(errorText(err), "error")); }} />
    </div>
  );
}
