import { Link } from "react-router-dom";
import { PeriodNote, Shell, State, riskLabel, selectClass, useExecutivePeriod, useOrganization } from "./shared";

const kinds = ["", "OverdueGoal", "PastTarget", "Blocked", "MissingPlan", "StaleProgress"];

export function ExecutiveRisks() {
  const period = useExecutivePeriod();
  const kind = period.status;
  const { data, error } = useOrganization(period.month, { departmentId: period.departmentId });
  const blocked = State({ error, data });
  if (blocked) return blocked;
  if (!data) return null;
  const rows = data.risks.filter((risk) => !kind || risk.kind === kind);

  return (
    <Shell
      title="Risks and Delays"
      subtitle={`A goal is overdue when its target date is before today and it is still open. Progress is stale after ${data.staleProgressDays} days without an update.`}
      crumbs={[{ label: "Executive Overview", to: period.href("/executive") }, { label: "Risks and Delays" }]}
      actions={
        <div className="flex flex-wrap gap-2">
          <input className={selectClass} type="month" value={period.month} onChange={(event) => period.setFilter("month", event.target.value)} aria-label="Reporting month" />
          <select className={selectClass} value={kind} onChange={(event) => period.setFilter("status", event.target.value)} aria-label="Risk type">
            {kinds.map((item) => <option key={item || "all"} value={item}>{item ? riskLabel(item) : "All attention items"}</option>)}
          </select>
        </div>
      }
    >
      <PeriodNote data={data} />
      {rows.length === 0 ? <p className="rounded-2xl border border-dashed border-line bg-card px-4 py-8 text-center text-sm text-muted">Nothing in this list needs attention.</p> : (
        <div className="space-y-2">
          {rows.map((risk, index) => (
            <article key={`${risk.kind}-${risk.title}-${index}`} className="rounded-2xl border border-line bg-card p-3 text-sm">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="font-semibold">{risk.title}</div>
                <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${risk.severity === "High" ? "bg-rose-50 text-rose-800" : "bg-amber-50 text-amber-950"}`}>{risk.severity === "High" ? "High" : "Watch"} · {riskLabel(risk.kind)}</span>
              </div>
              <div className="mt-1 text-muted">{risk.department ?? "No department"}{risk.project ? ` · ${risk.project}` : ""}{risk.due ? ` · due ${risk.due}` : ""}{risk.status ? ` · ${risk.status}` : ""}</div>
              <p className="mt-1">{risk.summary}</p>
              <div className="mt-2 flex flex-wrap gap-3">
                {risk.departmentId ? <Link className="font-semibold text-pine" to={period.href(`/executive/departments/${risk.departmentId}`, { departmentId: risk.departmentId })}>Department</Link> : null}
                {risk.projectId ? <Link className="font-semibold text-pine" to={period.href(`/executive/projects/${risk.projectId}`, { departmentId: risk.departmentId ?? "" })}>Project</Link> : null}
                {risk.userId ? <Link className="font-semibold text-pine" to={`/monthly-plan?userId=${risk.userId}`}>Monthly plan</Link> : null}
                {risk.userId ? <Link className="font-semibold text-pine" to={`/daily?userId=${risk.userId}`}>Authorized work details</Link> : null}
              </div>
            </article>
          ))}
        </div>
      )}
    </Shell>
  );
}
