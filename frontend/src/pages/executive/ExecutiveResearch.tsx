import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Loading, PageHeader, Stat } from "../../components/ui";
import { api, errorText } from "../../services/api";
import type { Department, ResearchPortfolio, WorkCategory } from "../../types";
import { currentMonthValue, label, parseMonth } from "../../utils/format";
import { useExecutivePeriod } from "./shared";

export function ExecutiveResearch() {
  const period = useExecutivePeriod();
  const [portfolio, setPortfolio] = useState<ResearchPortfolio | null>(null);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [categories, setCategories] = useState<WorkCategory[]>([]);
  const [status, setStatus] = useState("");
  const [priority, setPriority] = useState("");
  const [stage, setStage] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [error, setError] = useState("");
  const { year, month } = parseMonth(period.month || currentMonthValue());

  useEffect(() => {
    const params = new URLSearchParams({ year: String(year), month: String(month) });
    if (period.departmentId) params.set("departmentId", period.departmentId);
    if (categoryId) params.set("categoryId", categoryId);
    if (status) params.set("status", status);
    if (priority) params.set("priority", priority);
    if (stage) params.set("stage", stage);
    api<ResearchPortfolio>(`/api/initiatives/portfolio?${params}`).then(setPortfolio).catch((err) => setError(errorText(err)));
  }, [year, month, period.departmentId, categoryId, status, priority, stage]);

  useEffect(() => {
    api<Department[]>("/api/departments").then(setDepartments).catch(() => undefined);
    api<WorkCategory[]>("/api/work-categories").then(setCategories).catch(() => undefined);
  }, []);

  if (error) return <p className="text-sm text-rose-700">{error}</p>;
  if (!portfolio) return <Loading label="Loading the research portfolio" />;
  const visibleCategories = categories.filter((category) => !period.departmentId || category.departmentId === period.departmentId);

  return (
    <div>
      <PageHeader title="Research portfolio" subtitle={portfolio.progressRule} />
      <div className="mb-4 flex flex-wrap gap-2">
        <input className="rounded-xl border border-line bg-white px-3 py-2" type="month" value={period.month} onChange={(event) => period.setFilter("month", event.target.value)} />
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={period.departmentId} onChange={(event) => { period.setFilter("departmentId", event.target.value); setCategoryId(""); }}>
          <option value="">All departments</option>
          {departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}
        </select>
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
          <option value="">All categories</option>
          {visibleCategories.map((category) => <option key={category.id} value={category.id}>{category.departmentName} — {category.name}</option>)}
        </select>
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={status} onChange={(event) => setStatus(event.target.value)}><option value="">Any status</option>{["Planned", "Ongoing", "Completed", "OnHold", "Cancelled"].map((item) => <option key={item} value={item}>{label(item)}</option>)}</select>
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={priority} onChange={(event) => setPriority(event.target.value)}><option value="">Any priority</option>{["Low", "Medium", "High", "Critical"].map((item) => <option key={item}>{item}</option>)}</select>
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={stage} onChange={(event) => setStage(event.target.value)}><option value="">Any stage</option>{["Idea", "Research", "ProofOfConcept", "Validation", "Pilot", "Completed"].map((item) => <option key={item} value={item}>{label(item)}</option>)}</select>
      </div>
      <div className="mb-5 grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        <Stat label="Active" value={String(portfolio.activeInitiatives)} />
        <Stat label="On hold" value={String(portfolio.onHold)} />
        <Stat label="Completed this period" value={String(portfolio.completedInPeriod)} />
        <Stat label="Milestones due" value={String(portfolio.milestonesDue)} />
        <Stat label="Milestones overdue" value={String(portfolio.milestonesOverdue)} />
      </div>
      <div className="grid gap-4 lg:grid-cols-3">
        <CountList title="By status" rows={portfolio.byStatus} />
        <CountList title="By research stage" rows={portfolio.byStage} />
        <CountList title="By priority" rows={portfolio.byPriority} />
      </div>
      <section className="mt-5 rounded-2xl border border-line bg-card p-4">
        <h2 className="mb-3 font-display text-lg font-semibold">Category progress</h2>
        {portfolio.categories.length === 0 ? <p className="text-sm text-muted">No categorized initiatives match this view.</p> : (
          <table className="w-full text-left text-sm">
            <thead className="text-xs tracking-wide text-muted uppercase"><tr><th className="py-2">Department</th><th>Category</th><th>Completed</th><th>Counted</th><th>Completion</th></tr></thead>
            <tbody>
              {portfolio.categories.map((row) => (
                <tr key={row.categoryId} className="border-t border-line">
                  <td className="py-2"><Link className="font-semibold text-pine" to={period.href(`/executive/departments/${row.departmentId}`, { departmentId: row.departmentId })}>{row.departmentName}</Link></td>
                  <td>{row.categoryName}</td>
                  <td>{row.completed}</td>
                  <td>{row.counted}</td>
                  <td>{row.completionPercent === null ? "—" : `${row.completionPercent}%`}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
      <section className="mt-5 rounded-2xl border border-line bg-card p-4">
        <h2 className="mb-3 font-display text-lg font-semibold">Deadlines this period</h2>
        {portfolio.deadlines.length === 0 ? <p className="text-sm text-muted">No open deadlines in this period.</p> : portfolio.deadlines.map((item) => (
          <div key={item.id} className="border-t border-line py-2 text-sm"><span className="font-semibold">{item.departmentName} — {item.workCategoryName ?? "No category"} — {item.number}</span> {item.title} · due {item.targetEnd}</div>
        ))}
      </section>
      <section className="mt-5 rounded-2xl border border-line bg-card p-4">
        <h2 className="mb-3 font-display text-lg font-semibold">Recorded outcomes</h2>
        {portfolio.outcomes.length === 0 ? <p className="text-sm text-muted">No validation outcomes were recorded in this period.</p> : portfolio.outcomes.map((record) => (
          <article key={record.id} className="border-t border-line py-2 text-sm"><div className="font-semibold">{record.title}</div><p>{record.outcome}</p></article>
        ))}
      </section>
    </div>
  );
}

function CountList({ title, rows }: { title: string; rows: { name: string; count: number }[] }) {
  return (
    <section className="rounded-2xl border border-line bg-card p-4">
      <h2 className="mb-2 font-semibold">{title}</h2>
      {rows.length === 0 ? <p className="text-sm text-muted">None</p> : rows.map((row) => <div key={row.name} className="flex justify-between text-sm"><span>{label(row.name)}</span><span>{row.count}</span></div>)}
    </section>
  );
}
