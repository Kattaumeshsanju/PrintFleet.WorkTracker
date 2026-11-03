import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Badge } from "../../components/ui";
import { api, errorText } from "../../services/api";
import type { Department, TeamPlans } from "../../types";
import { label, monthLabel, parseMonth } from "../../utils/format";
import { Meter, PeriodNote, Shell, State, selectClass, useExecutivePeriod, useOrganization } from "./shared";
import { ProjectTable } from "./ExecutiveDepartments";

export function ExecutiveProjects() {
  const period = useExecutivePeriod();
  const { data, error } = useOrganization(period.month, { projectStatus: period.projectStatus, dueBefore: period.dueBefore, departmentId: period.departmentId });
  const [departments, setDepartments] = useState<Department[]>([]);
  useEffect(() => { api<Department[]>("/api/departments").then(setDepartments).catch(() => undefined); }, []);
  const blocked = State({ error, data });
  if (blocked) return blocked;
  if (!data) return null;

  return (
    <Shell
      title="Projects"
      subtitle="Project progress is the average of each goal's progress. It is not the same figure as monthly goal completion."
      crumbs={[{ label: "Executive Overview", to: period.href("/executive") }, { label: "Projects" }]}
      actions={
        <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
          <input className={selectClass} type="month" value={period.month} onChange={(event) => period.setFilter("month", event.target.value)} aria-label="Reporting month" />
          <select className={selectClass} value={period.departmentId} onChange={(event) => period.setFilter("departmentId", event.target.value)} aria-label="Department">
            <option value="">All departments</option>
            {departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}
          </select>
          <select className={selectClass} value={period.projectStatus} onChange={(event) => period.setFilter("projectStatus", event.target.value)} aria-label="Project status">
            <option value="">All statuses</option>
            {["Active", "OnHold", "Completed", "Cancelled"].map((status) => <option key={status} value={status}>{label(status)}</option>)}
          </select>
          <input className={selectClass} type="date" value={period.dueBefore} onChange={(event) => period.setFilter("dueBefore", event.target.value)} aria-label="Target on or before" />
        </div>
      }
    >
      <PeriodNote data={data} />
      {data.projects.length === 0 ? <p className="rounded-2xl border border-dashed border-line bg-card px-4 py-8 text-center text-sm text-muted">No projects match these filters.</p> : (
        <div className="rounded-2xl border border-line bg-card p-4"><ProjectTable data={data} periodHref={period.href} /></div>
      )}
    </Shell>
  );
}

export function ExecutiveProject() {
  const { projectId = "" } = useParams();
  const period = useExecutivePeriod();
  const { data, error } = useOrganization(period.month, { departmentId: period.departmentId });
  const [plans, setPlans] = useState<TeamPlans | null>(null);
  const [planError, setPlanError] = useState("");

  useEffect(() => {
    const { year, month } = parseMonth(period.month);
    const query = new URLSearchParams({ year: String(year), month: String(month), projectId });
    if (period.departmentId) query.set("departmentId", period.departmentId);
    api<TeamPlans>(`/api/monthly-work/team?${query}`).then(setPlans).catch((err) => setPlanError(errorText(err)));
  }, [period.month, period.departmentId, projectId]);

  const blocked = State({ error, data });
  if (blocked) return blocked;
  if (!data) return null;
  const project = data.projects.find((item) => item.projectId === projectId);
  if (!project) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">That project was not found for this period.</div>;

  return (
    <Shell
      title={project.name}
      subtitle={`${project.departmentName ?? "No department"} · ${project.ownerName ?? "No owner"}`}
      crumbs={[
        { label: "Executive Overview", to: period.href("/executive") },
        { label: "Projects", to: period.href("/executive/projects") },
        { label: project.name },
      ]}
      actions={<input className={selectClass} type="month" value={period.month} onChange={(event) => period.setFilter("month", event.target.value)} aria-label="Reporting month" />}
    >
      <PeriodNote data={data} />
      <div className="flex flex-wrap items-center gap-3 text-sm">
        <Badge value={project.status} />
        <span>Start {project.startDate ?? "—"}</span>
        <span>Target {project.endDate ?? "—"}</span>
        {project.atRisk ? <span className="rounded-full bg-rose-50 px-2 py-0.5 text-rose-800">{project.riskSummary}</span> : <span className="text-muted">Not at risk</span>}
      </div>
      <Meter value={project.progressPercent} ready={project.hasGoals} caption="Share of this project's planned hours marked done" />
      <section className="rounded-2xl border border-line bg-card p-4">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="font-display text-lg font-semibold">Monthly goals</h2>
          <Link className="text-sm font-semibold text-pine" to={period.href("/executive/plans", { departmentId: project.departmentId ?? "" })}>Department monthly plan</Link>
        </div>
        {planError ? <p className="text-sm text-rose-800">{planError}</p> : null}
        {!plans ? <p className="text-sm text-muted">Loading goals…</p> : plans.employees.every((employee) => employee.tasks.length === 0) ? <p className="text-sm text-muted">No monthly goals are linked to this project for {monthLabel(plans.year, plans.month)}.</p> : (
          <div className="space-y-4">
            {plans.employees.filter((employee) => employee.tasks.length > 0).map((employee) => (
              <div key={employee.user.id}>
                <div className="mb-1 text-sm font-semibold">{employee.user.name}</div>
                <ul className="space-y-1 text-sm">
                  {employee.tasks.map((task) => (
                    <li key={task.id} className="flex flex-wrap items-center justify-between gap-2 border-t border-line py-2">
                      <span>{task.title}</span>
                      <span className="text-muted">{label(task.status)} · {task.progressPercent}% · due {task.plannedEndDate}</span>
                    </li>
                  ))}
                </ul>
                <Link className="text-sm font-semibold text-pine" to={`/monthly-plan?userId=${employee.user.id}`}>Open monthly plan</Link>
                <Link className="ml-3 text-sm font-semibold text-pine" to={`/daily?userId=${employee.user.id}`}>Authorized work details</Link>
              </div>
            ))}
          </div>
        )}
      </section>
    </Shell>
  );
}
