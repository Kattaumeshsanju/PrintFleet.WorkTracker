import { Link, useParams } from "react-router-dom";
import { Badge } from "../../components/ui";
import type { OrganizationDashboard } from "../../types";
import { Meter, PeriodNote, Shell, State, selectClass, useExecutivePeriod, useOrganization } from "./shared";

export function ExecutiveDepartments() {
  const period = useExecutivePeriod();
  const { data, error } = useOrganization(period.month);
  const blocked = State({ error, data });
  if (blocked) return blocked;
  if (!data) return null;

  const rows = [...data.departments].sort((a, b) => {
    if (period.sort === "overdue") return b.overdueGoals - a.overdueGoals || a.name.localeCompare(b.name);
    if (period.sort === "projects") return b.activeProjects - a.activeProjects || a.name.localeCompare(b.name);
    if (period.sort === "progress") return (b.hasGoals ? b.completionPercent : -1) - (a.hasGoals ? a.completionPercent : -1);
    return a.name.localeCompare(b.name);
  });

  return (
    <Shell
      title="Departments"
      subtitle="Each department keeps its own projects and monthly plan."
      crumbs={[{ label: "Executive Overview", to: period.href("/executive") }, { label: "Departments" }]}
      actions={
        <div className="flex flex-wrap gap-2">
          <input className={selectClass} type="month" value={period.month} onChange={(event) => period.setFilter("month", event.target.value)} aria-label="Reporting month" />
          <select className={selectClass} value={period.sort} onChange={(event) => period.setFilter("sort", event.target.value)} aria-label="Sort departments">
            <option value="name">Name</option>
            <option value="progress">Progress</option>
            <option value="overdue">Overdue work</option>
            <option value="projects">Active projects</option>
          </select>
        </div>
      }
    >
      <PeriodNote data={data} />
      {rows.length === 0 ? <Empty text="No departments are available." /> : (
        <div className="overflow-x-auto rounded-2xl border border-line bg-card">
          <table className="w-full min-w-[760px] text-left text-sm">
            <thead className="text-xs text-muted uppercase"><tr>{["Department", "Team lead", "Projects", "Planned", "Done", "In progress", "Overdue", "Blockers", "Progress"].map((heading) => <th key={heading} className="px-3 py-2">{heading}</th>)}</tr></thead>
            <tbody>
              {rows.map((department) => (
                <tr key={department.departmentId} className="border-t border-line">
                  <td className="px-3 py-2 font-semibold"><Link to={period.href(`/executive/departments/${department.departmentId}`, { departmentId: department.departmentId })}>{department.name}</Link>{department.isActive ? null : <div className="text-xs text-muted">Inactive</div>}</td>
                  <td>{department.teamLeadName ?? "—"}</td>
                  <td>{department.activeProjects}</td>
                  <td>{department.plannedGoals}{department.hasMonthlyPlan ? null : <div className="text-xs text-muted">No department plan</div>}</td>
                  <td>{department.hasGoals ? department.completedGoals : "—"}</td>
                  <td>{department.hasGoals ? department.inProgressGoals : "—"}</td>
                  <td>{department.overdueGoals}</td>
                  <td>{department.blockedGoals}</td>
                  <td className="px-3 py-2"><Meter value={department.completionPercent} ready={department.hasGoals} caption="Goals" /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Shell>
  );
}

export function ExecutiveDepartment() {
  const { departmentId = "" } = useParams();
  const period = useExecutivePeriod();
  const { data, error } = useOrganization(period.month, { departmentId });
  const blocked = State({ error, data });
  if (blocked) return blocked;
  if (!data) return null;
  const department = data.departments.find((item) => item.departmentId === departmentId);
  if (!department) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">That department was not found.</div>;

  return (
    <Shell
      title={department.name}
      subtitle={`${department.teamLeadName ?? "No team lead"} · ${department.activeProjects} active projects`}
      crumbs={[
        { label: "Executive Overview", to: period.href("/executive") },
        { label: "Departments", to: period.href("/executive/departments", { departmentId: "" }) },
        { label: department.name },
      ]}
      actions={<input className={selectClass} type="month" value={period.month} onChange={(event) => period.setFilter("month", event.target.value)} aria-label="Reporting month" />}
    >
      <PeriodNote data={data} />
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Stat label="Planned goals" value={department.hasMonthlyPlan ? String(department.plannedGoals) : "No plan"} />
        <Stat label="Completed" value={department.hasGoals ? String(department.completedGoals) : "—"} />
        <Stat label="In progress" value={department.hasGoals ? String(department.inProgressGoals) : "—"} />
        <Stat label="Overdue / blocked" value={`${department.overdueGoals} / ${department.blockedGoals}`} />
      </div>
      <Meter value={department.completionPercent} ready={department.hasGoals} caption="Share of planned hours marked done" />
      <section className="rounded-2xl border border-line bg-card p-4">
        <h2 className="mb-3 font-display text-lg font-semibold">Projects</h2>
        {data.projects.length === 0 ? <Empty text="This department has no projects for the current filters." /> : <ProjectTable data={data} periodHref={period.href} />}
      </section>
      <section className="rounded-2xl border border-line bg-card p-4">
        <div className="mb-2 flex items-center justify-between">
          <h2 className="font-display text-lg font-semibold">Monthly plan</h2>
          <Link className="text-sm font-semibold text-pine" to={period.href("/executive/plans", { departmentId })}>Open monthly plans</Link>
        </div>
        {data.plans.length === 0 ? <p className="text-sm text-muted">No department plan is open for this month.</p> : data.plans.map((plan) => (
          <div key={plan.id} className="text-sm">
            <div className="mb-1 flex items-center gap-2"><Badge value={plan.status} /></div>
            <p>{plan.summary || "No summary yet."}</p>
            <p className="mt-2 text-muted">Risks: {plan.risks || "None recorded."}</p>
          </div>
        ))}
      </section>
    </Shell>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return <div className="rounded-2xl border border-line bg-card px-4 py-3"><div className="text-xs text-muted uppercase">{label}</div><div className="mt-1 font-display text-xl font-semibold">{value}</div></div>;
}

function Empty({ text }: { text: string }) {
  return <p className="text-sm text-muted">{text}</p>;
}

export function ProjectTable({ data, periodHref }: { data: OrganizationDashboard; periodHref: (path: string, extra?: Record<string, string>) => string }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[720px] text-left text-sm">
        <thead className="text-xs text-muted uppercase"><tr>{["Project", "Owner", "Status", "Target", "Progress", "Risk"].map((heading) => <th key={heading} className="px-3 py-2">{heading}</th>)}</tr></thead>
        <tbody>
          {data.projects.map((project) => (
            <tr key={project.projectId} className="border-t border-line">
              <td className="px-3 py-2 font-semibold"><Link to={periodHref(`/executive/projects/${project.projectId}`, { departmentId: project.departmentId ?? "" })}>{project.name}</Link><div className="font-normal text-muted">{project.departmentName ?? "No department"}</div></td>
              <td>{project.ownerName ?? "—"}</td>
              <td><Badge value={project.status} /></td>
              <td>{project.endDate ?? "—"}</td>
              <td><Meter value={project.progressPercent} ready={project.hasGoals} caption="Goal progress" /></td>
              <td className="text-muted">{project.atRisk ? project.riskSummary : "—"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
