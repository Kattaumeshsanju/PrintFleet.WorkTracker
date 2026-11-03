import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Badge } from "../../components/ui";
import { api, errorText } from "../../services/api";
import type { Department, TeamPlans } from "../../types";
import { label } from "../../utils/format";
import { Meter, PeriodNote, Shell, State, selectClass, useExecutivePeriod, useOrganization } from "./shared";

export function ExecutivePlans() {
  const period = useExecutivePeriod();
  const { data, error } = useOrganization(period.month, { departmentId: period.departmentId, status: period.status });
  const [plans, setPlans] = useState<TeamPlans | null>(null);
  const [planError, setPlanError] = useState("");
  const [departments, setDepartments] = useState<Department[]>([]);

  useEffect(() => { api<Department[]>("/api/departments").then(setDepartments).catch(() => undefined); }, []);
  useEffect(() => {
    const [year, month] = period.month.split("-").map(Number);
    const query = new URLSearchParams({ year: String(year), month: String(month) });
    if (period.departmentId) query.set("departmentId", period.departmentId);
    if (period.status) query.set("status", period.status);
    api<TeamPlans>(`/api/monthly-work/team?${query}`).then(setPlans).catch((err) => setPlanError(errorText(err)));
  }, [period.month, period.departmentId, period.status]);

  const blocked = State({ error, data });
  if (blocked) return blocked;
  if (!data) return null;

  return (
    <Shell
      title="Monthly Plans"
      subtitle="Planned goals for the month. Daily timelines are not part of this summary."
      crumbs={[{ label: "Executive Overview", to: period.href("/executive") }, { label: "Monthly Plans" }]}
      actions={
        <div className="flex flex-wrap gap-2">
          <input className={selectClass} type="month" value={period.month} onChange={(event) => period.setFilter("month", event.target.value)} aria-label="Reporting month" />
          <select className={selectClass} value={period.departmentId} onChange={(event) => period.setFilter("departmentId", event.target.value)} aria-label="Department">
            <option value="">All departments</option>
            {departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}
          </select>
          <select className={selectClass} value={period.status} onChange={(event) => period.setFilter("status", event.target.value)} aria-label="Goal status">
            <option value="">All goal statuses</option>
            {["Planned", "InProgress", "Completed", "Blocked", "Cancelled"].map((status) => <option key={status} value={status}>{label(status)}</option>)}
          </select>
        </div>
      }
    >
      <PeriodNote data={data} />
      <div className="grid gap-3 sm:grid-cols-3">
        <div className="rounded-2xl border border-line bg-card p-4"><div className="text-xs text-muted uppercase">Planned vs completed</div><div className="mt-1 font-display text-2xl">{data.completedGoals} / {data.plannedGoals}</div></div>
        <div className="rounded-2xl border border-line bg-card p-4"><Meter value={data.completionPercent} ready={data.hasGoals} caption="Goal completion" /></div>
        <div className="rounded-2xl border border-line bg-card p-4"><div className="text-xs text-muted uppercase">Overdue or blocked</div><div className="mt-1 font-display text-2xl">{data.overdueGoals} / {data.blockedGoals}</div></div>
      </div>
      {planError ? <p className="text-sm text-rose-800">{planError}</p> : null}
      {data.departments.filter((department) => !period.departmentId || department.departmentId === period.departmentId).map((department) => (
        <section key={department.departmentId} className="rounded-2xl border border-line bg-card p-4">
          <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
            <Link className="font-display text-lg font-semibold" to={period.href(`/executive/departments/${department.departmentId}`, { departmentId: department.departmentId })}>{department.name}</Link>
            {department.hasMonthlyPlan ? <Badge value="Open" /> : <span className="text-sm text-muted">No department plan</span>}
          </div>
          {!plans ? <p className="text-sm text-muted">Loading goals…</p> : plans.employees.filter((employee) => employee.user.departmentId === department.departmentId).length === 0 ? <p className="text-sm text-muted">No goals match this department.</p> : plans.employees.filter((employee) => employee.user.departmentId === department.departmentId).map((employee) => (
            <div key={employee.user.id} className="border-t border-line py-2 text-sm">
              <div className="font-semibold">{employee.user.name} · {employee.plannedTasks === 0 ? "No counted goals" : `${employee.completed}/${employee.plannedTasks} complete`}</div>
              {employee.tasks.filter((task) => task.status === "Blocked" || task.overdue).map((task) => (
                <div key={task.id} className="text-muted">{task.title} · {label(task.status)} · due {task.plannedEndDate}{task.remarks ? ` · ${task.remarks}` : ""}</div>
              ))}
              <Link className="font-semibold text-pine" to={`/monthly-plan?userId=${employee.user.id}`}>Monthly plan</Link>
            </div>
          ))}
        </section>
      ))}
    </Shell>
  );
}
