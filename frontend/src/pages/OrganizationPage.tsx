import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { Badge, Loading, PageHeader, Panel, Stat } from "../components/ui";
import { api, errorText } from "../services/api";
import type { Department, OrganizationDashboard, Project, User } from "../types";
import { currentMonthValue, label, monthLabel, parseMonth } from "../utils/format";

const selectClass = "rounded-xl border border-line bg-white px-3 py-2";

export function OrganizationPage() {
  const [params, setParams] = useSearchParams();
  const month = params.get("month") || currentMonthValue();
  const departmentId = params.get("departmentId") ?? "";
  const projectId = params.get("projectId") ?? "";
  const status = params.get("status") ?? "";
  const userId = params.get("userId") ?? "";
  const [data, setData] = useState<OrganizationDashboard | null>(null);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [projects, setProjects] = useState<Project[]>([]);
  const [people, setPeople] = useState<User[]>([]);
  const [error, setError] = useState("");

  function setFilter(key: string, value: string) {
    const next = new URLSearchParams(params);
    if (value) next.set(key, value);
    else next.delete(key);
    setParams(next, { replace: true });
  }

  useEffect(() => {
    api<Department[]>("/api/departments").then(setDepartments).catch(() => undefined);
    api<User[]>("/api/users").then(setPeople).catch(() => undefined);
  }, []);

  useEffect(() => {
    const query = departmentId ? `?departmentId=${departmentId}` : "";
    api<Project[]>(`/api/projects${query}`).then(setProjects).catch(() => undefined);
  }, [departmentId]);

  useEffect(() => {
    const { year, month: monthNumber } = parseMonth(month);
    const query = new URLSearchParams({ year: String(year), month: String(monthNumber) });
    if (departmentId) query.set("departmentId", departmentId);
    if (projectId) query.set("projectId", projectId);
    if (status) query.set("status", status);
    if (userId) query.set("userId", userId);
    api<OrganizationDashboard>(`/api/organization?${query}`)
      .then(setData)
      .catch((err) => setError(errorText(err)));
  }, [month, departmentId, projectId, status, userId]);

  if (error) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;
  if (!data) return <Loading />;

  const period = `${monthLabel(data.year, data.month)}`;
  const planQuery = `month=${month}${departmentId ? `&departmentId=${departmentId}` : ""}`;

  return (
    <div className="space-y-5">
      <PageHeader title="Organization Overview" subtitle={`${period} · every department, without changing your role. Daily timelines stay on the work-detail link.`} />
      <div className="grid gap-2 md:grid-cols-3 xl:grid-cols-5">
        <input className={selectClass} type="month" value={month} onChange={(event) => setFilter("month", event.target.value)} />
        <select className={selectClass} value={departmentId} onChange={(event) => setFilter("departmentId", event.target.value)}>
          <option value="">All departments</option>
          {departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}
        </select>
        <select className={selectClass} value={projectId} onChange={(event) => setFilter("projectId", event.target.value)}>
          <option value="">All projects</option>
          {projects.map((project) => <option key={project.id} value={project.id}>{project.departmentName ? `${project.departmentName} · ` : ""}{project.name}</option>)}
        </select>
        <select className={selectClass} value={status} onChange={(event) => setFilter("status", event.target.value)}>
          <option value="">All goal statuses</option>
          {["Planned", "InProgress", "Completed", "Blocked", "Cancelled"].map((item) => <option key={item} value={item}>{label(item)}</option>)}
        </select>
        <select className={selectClass} value={userId} onChange={(event) => setFilter("userId", event.target.value)}>
          <option value="">All employees</option>
          {people.filter((person) => person.role !== "Admin" && person.role !== "CEO").map((person) => <option key={person.id} value={person.id}>{person.name}{person.departmentName ? ` · ${person.departmentName}` : ""}</option>)}
        </select>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Stat label="Departments" value={String(data.departmentCount)} />
        <Stat label="Active projects" value={String(data.activeProjectCount)} />
        <Stat label="Planned vs completed" value={`${data.completedGoals} / ${data.plannedGoals}`} />
        <Stat label="Plan completion" value={data.hasGoals ? `${data.completionPercent}%` : "No goals"} />
        <Stat label="Overdue goals" value={String(data.overdueGoals)} />
        <Stat label="Blocked goals" value={String(data.blockedGoals)} />
      </div>

      <Panel title="Department progress">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[760px] text-left text-sm">
            <thead className="text-xs text-muted uppercase"><tr><th className="py-2">Department</th><th>Projects</th><th>Planned</th><th>Completed</th><th>Blocked</th><th>Overdue</th><th>Completion</th></tr></thead>
            <tbody>
              {data.departments.map((department) => (
                <tr key={department.departmentId} className="border-t border-line">
                  <td className="py-2 font-semibold"><Link to={`/plans?month=${month}&departmentId=${department.departmentId}`}>{department.name}</Link>{department.isActive ? null : <span className="ml-2 text-xs text-muted">Inactive</span>}</td>
                  <td>{department.activeProjects}</td>
                  <td>{department.plannedGoals}</td>
                  <td>{department.completedGoals}</td>
                  <td>{department.blockedGoals}</td>
                  <td>{department.overdueGoals}</td>
                  <td>{department.hasGoals ? `${department.completionPercent}%` : "No goals"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Panel>

      <Panel title="Projects">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[860px] text-left text-sm">
            <thead className="text-xs text-muted uppercase"><tr><th className="py-2">Project</th><th>Department</th><th>Owner</th><th>Status</th><th>Priority</th><th>Progress</th><th>Goals</th><th>Target</th></tr></thead>
            <tbody>
              {data.projects.map((project) => (
                <tr key={project.projectId} className="border-t border-line">
                  <td className="py-2 font-semibold"><Link to={`/projects?departmentId=${project.departmentId ?? ""}`}>{project.name}</Link></td>
                  <td>{project.departmentName ?? "—"}</td>
                  <td>{project.ownerName ?? "—"}</td>
                  <td><Badge value={project.status} /></td>
                  <td>{project.priority}</td>
                  <td>{project.hasGoals ? `${project.progressPercent}%` : "No goals"}</td>
                  <td>{project.completedGoals} / {project.plannedGoals}</td>
                  <td>{project.endDate ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Panel>

      <div className="grid gap-4 xl:grid-cols-3">
        <AlertList title="Overdue" rows={data.overdue} />
        <AlertList title="Due in the next 7 days" rows={data.upcoming} />
        <AlertList title="Blocked goals" rows={data.blockers} />
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Panel title="Workload">
          {data.workload.length === 0 ? <div className="text-sm text-muted">No goals match these filters.</div> : (
            <table className="w-full text-left text-sm">
              <thead className="text-xs text-muted uppercase"><tr><th className="py-2">Employee</th><th>Department</th><th>Goals</th><th>Done</th><th>Blocked</th></tr></thead>
              <tbody>
                {data.workload.map((row) => (
                  <tr key={row.userId} className="border-t border-line">
                    <td className="py-2 font-semibold"><Link to={`/monthly-plan?userId=${row.userId}`}>{row.name}</Link></td>
                    <td>{row.department ?? "—"}</td>
                    <td>{row.goals}</td>
                    <td>{row.completed}</td>
                    <td>{row.blocked}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Panel>
        <Panel title="Six-month completion">
          <div className="h-64">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={data.trend.map((point) => ({ name: monthLabel(point.year, point.month).split(" ")[0], planned: point.plannedGoals, completed: point.completedGoals }))}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} />
                <XAxis dataKey="name" />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="planned" fill="#d5cec2" name="Planned" />
                <Bar dataKey="completed" fill="#1d6b60" name="Completed" />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </Panel>
      </div>

      <Panel title="Department plan notes">
        {data.plans.length === 0 ? <div className="text-sm text-muted">No department plan has been opened for this month.</div> : (
          <div className="grid gap-3 md:grid-cols-2">
            {data.plans.map((plan) => (
              <div key={plan.id} className="rounded-xl bg-slate-50 p-3 text-sm">
                <div className="mb-1 flex items-center justify-between gap-2">
                  <Link className="font-semibold" to={`/plans?${planQuery}&departmentId=${plan.departmentId}`}>{plan.departmentName}</Link>
                  <Badge value={plan.status} />
                </div>
                <div>{plan.summary || "No summary yet."}</div>
                <div className="mt-2 text-muted">Risks: {plan.risks || "None recorded."}</div>
              </div>
            ))}
          </div>
        )}
      </Panel>
    </div>
  );
}

function AlertList({ title, rows }: { title: string; rows: OrganizationDashboard["overdue"] }) {
  return (
    <Panel title={title}>
      {rows.length === 0 ? <div className="text-sm text-muted">None.</div> : (
        <div className="space-y-2">
          {rows.map((row) => (
            <div key={row.taskId} className="rounded-xl border border-line px-3 py-2 text-sm">
              <div className="font-semibold">{row.title}</div>
              <div className="text-muted">{row.department} · {row.project ?? "No project"} · due {row.due}</div>
              <div className="mt-1 flex flex-wrap gap-3">
                <Link className="font-semibold text-pine" to={`/monthly-plan?userId=${row.userId}`}>{row.employee}</Link>
                <Link className="font-semibold text-pine" to={`/daily?userId=${row.userId}`}>Work details</Link>
              </div>
              {row.remarks ? <div className="mt-1 text-muted">{row.remarks}</div> : null}
            </div>
          ))}
        </div>
      )}
    </Panel>
  );
}
