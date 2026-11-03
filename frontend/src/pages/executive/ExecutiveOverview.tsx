import { Link } from "react-router-dom";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { monthLabel } from "../../utils/format";
import { Kpi, Meter, PeriodNote, Shell, State, changeText, selectClass, useExecutivePeriod, useOrganization } from "./shared";

export function ExecutiveOverview() {
  const period = useExecutivePeriod();
  const { data, error } = useOrganization(period.month);
  const blocked = State({ error, data });
  if (blocked) return blocked;
  if (!data) return null;

  const rows = [...data.departments].sort((a, b) => a.name.localeCompare(b.name)).slice(0, 6);
  const attention = data.risks.filter((risk) => risk.severity === "High").slice(0, 5);

  return (
    <Shell
      title="Executive Overview"
      subtitle="Organization progress for the selected month. This view does not change people, departments, or plans."
      crumbs={[{ label: "Executive Overview" }]}
      actions={<input className={selectClass} type="month" value={period.month} onChange={(event) => period.setFilter("month", event.target.value)} aria-label="Reporting month" />}
    >
      <PeriodNote data={data} />
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        <Kpi label="Departments" value={String(data.departmentCount)} hint="Active departments. Inactive departments stay in the department list." />
        <Kpi label="Active projects" value={String(data.activeProjectCount)} hint="Projects whose status is Active. This is separate from monthly goal completion." />
        <Kpi label="Goals planned" value={String(data.plannedGoals)} hint={data.cancelledGoals > 0 ? `Goals that still count. ${data.cancelledGoals} cancelled ${data.cancelledGoals === 1 ? "goal is" : "goals are"} excluded from this number and from the completion rate.` : "Goals that still count. Cancelled goals are excluded from this number and from the completion rate."} />
        <Kpi label="Goals completed" value={data.hasGoals ? `${data.completedGoals} · ${data.completionPercent}%` : "No goals"} hint={data.hasGoals ? changeText(data.completionChange) : "No goals are counted, so this is not shown as 0%."} />
        <Kpi label="Overdue goals" value={String(data.overdueGoals)} hint="Open goals whose target date is before today." />
        <Kpi label="Projects at risk" value={String(data.projectsAtRisk)} hint={data.progressRule} />
      </div>
      <section className="rounded-2xl border border-line bg-card p-4">
        <h2 className="font-display text-lg font-semibold">Six-month goal completion</h2>
        <p className="mb-3 text-sm text-muted">Cancelled goals are left out. A month with no counted goals is not scored as 0%.</p>
        <div className="h-52">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={data.trend.map((point) => ({ name: monthLabel(point.year, point.month).split(" ")[0], planned: point.plannedGoals, completed: point.completedGoals }))}>
              <CartesianGrid strokeDasharray="3 3" vertical={false} />
              <XAxis dataKey="name" />
              <YAxis allowDecimals={false} />
              <Tooltip />
              <Bar dataKey="planned" fill="#d5cec2" name="Counted goals" />
              <Bar dataKey="completed" fill="#1d6b60" name="Completed" />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </section>
      <section className="rounded-2xl border border-line bg-card p-4">
        <div className="mb-3 flex items-center justify-between gap-3">
          <h2 className="font-display text-lg font-semibold">Departments</h2>
          <Link className="text-sm font-semibold text-pine" to={period.href("/executive/departments")}>All departments</Link>
        </div>
        {rows.length === 0 ? <p className="text-sm text-muted">No departments yet.</p> : (
          <div className="grid gap-3 md:grid-cols-2">
            {rows.map((department) => (
              <Link key={department.departmentId} className="rounded-xl border border-line p-3 hover:bg-slate-50" to={period.href(`/executive/departments/${department.departmentId}`, { departmentId: department.departmentId })}>
                <div className="flex items-center justify-between gap-2">
                  <div className="font-semibold">{department.name}</div>
                  {!department.isActive ? <span className="text-xs text-muted">Inactive</span> : null}
                </div>
                <div className="mt-1 text-xs text-muted">{department.teamLeadName ?? "No team lead"} · {department.activeProjects} active projects</div>
                <div className="mt-3"><Meter value={department.completionPercent} ready={department.hasGoals} caption="Goal completion" /></div>
              </Link>
            ))}
          </div>
        )}
      </section>
      <section className="rounded-2xl border border-line bg-card p-4">
        <div className="mb-3 flex items-center justify-between gap-3">
          <h2 className="font-display text-lg font-semibold">Needs attention</h2>
          <Link className="text-sm font-semibold text-pine" to={period.href("/executive/risks")}>Risks and delays</Link>
        </div>
        {attention.length === 0 ? <p className="text-sm text-muted">Nothing is overdue, blocked, or past its target date.</p> : (
          <ul className="space-y-2 text-sm">
            {attention.map((risk, index) => (
              <li key={`${risk.kind}-${risk.title}-${index}`} className="flex flex-wrap items-baseline justify-between gap-2 border-t border-line py-2">
                <span className="font-medium">{risk.title}</span>
                <span className="text-muted">{risk.department ?? "No department"} · {risk.summary}</span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </Shell>
  );
}
