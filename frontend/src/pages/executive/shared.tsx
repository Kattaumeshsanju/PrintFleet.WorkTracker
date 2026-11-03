import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Loading } from "../../components/ui";
import { api, errorText } from "../../services/api";
import type { OrganizationDashboard } from "../../types";
import { currentMonthValue, label, monthLabel, parseMonth } from "../../utils/format";

export function useExecutivePeriod() {
  const [params, setParams] = useSearchParams();
  const month = params.get("month") || currentMonthValue();
  const departmentId = params.get("departmentId") ?? "";
  const projectStatus = params.get("projectStatus") ?? "";
  const status = params.get("status") ?? "";
  const dueBefore = params.get("dueBefore") ?? "";
  const sort = params.get("sort") ?? "name";

  function setFilter(key: string, value: string) {
    const next = new URLSearchParams(params);
    if (!next.get("month")) next.set("month", month);
    if (value) next.set(key, value);
    else next.delete(key);
    setParams(next, { replace: true });
  }

  function href(path: string, extra?: Record<string, string>) {
    const next = new URLSearchParams(params);
    if (!next.get("month")) next.set("month", month);
    Object.entries(extra ?? {}).forEach(([key, value]) => {
      if (value) next.set(key, value);
      else next.delete(key);
    });
    const text = next.toString();
    return text ? `${path}?${text}` : path;
  }

  return { month, departmentId, projectStatus, status, dueBefore, sort, setFilter, href };
}

export function useOrganization(month: string, filters?: { departmentId?: string; projectStatus?: string; status?: string; dueBefore?: string }, enabled = true) {
  const [data, setData] = useState<OrganizationDashboard | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!enabled) return;
    const { year, month: monthNumber } = parseMonth(month);
    const query = new URLSearchParams({ year: String(year), month: String(monthNumber) });
    if (filters?.departmentId) query.set("departmentId", filters.departmentId);
    if (filters?.projectStatus) query.set("projectStatus", filters.projectStatus);
    if (filters?.status) query.set("status", filters.status);
    if (filters?.dueBefore) query.set("dueBefore", filters.dueBefore);
    api<OrganizationDashboard>(`/api/organization?${query}`).then(setData).catch((err) => setError(errorText(err)));
  }, [month, filters?.departmentId, filters?.projectStatus, filters?.status, filters?.dueBefore, enabled]);

  return { data, error };
}

export function Crumbs({ items }: { items: { label: string; to?: string }[] }) {
  return (
    <nav className="mb-3 flex flex-wrap items-center gap-1 text-sm text-muted" aria-label="Breadcrumb">
      {items.map((item, index) => (
        <span key={`${item.label}-${index}`} className="flex items-center gap-1">
          {index > 0 ? <span aria-hidden="true">/</span> : null}
          {item.to ? <Link className="font-semibold text-pine" to={item.to}>{item.label}</Link> : <span className="text-ink">{item.label}</span>}
        </span>
      ))}
    </nav>
  );
}

export function PeriodNote({ data }: { data: OrganizationDashboard }) {
  const when = new Date(data.generatedAt).toLocaleString(undefined, { hour: "2-digit", minute: "2-digit", day: "numeric", month: "short" });
  return <p className="text-sm text-muted">{monthLabel(data.year, data.month)} · refreshed {when}. Read only.</p>;
}

export function Kpi({ label: text, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="rounded-2xl border border-line bg-card px-4 py-3" title={hint}>
      <div className="text-xs font-semibold tracking-[0.14em] text-muted uppercase">{text}</div>
      <div className="mt-2 font-display text-2xl font-semibold">{value}</div>
      {hint ? <div className="mt-1 text-xs text-muted">{hint}</div> : null}
    </div>
  );
}

export function Meter({ value, ready, caption }: { value: number; ready: boolean; caption: string }) {
  if (!ready) return <span className="text-sm text-muted" title="No goals are counted for this period. Missing plans are not treated as zero.">No goals</span>;
  return (
    <div className="min-w-32" title={caption}>
      <div className="mb-1 flex justify-between text-xs"><span>{caption}</span><span>{value}%</span></div>
      <div className="h-1.5 overflow-hidden rounded-full bg-stone-200" role="meter" aria-label={caption} aria-valuenow={value} aria-valuemin={0} aria-valuemax={100}>
        <div className="h-1.5 rounded-full bg-[#1d6b60]" style={{ width: `${Math.max(0, Math.min(100, value))}%` }} />
      </div>
    </div>
  );
}

export function Shell({ title, subtitle, crumbs, children, actions }: { title: string; subtitle?: string; crumbs: { label: string; to?: string }[]; children: ReactNode; actions?: ReactNode }) {
  return (
    <div className="space-y-4">
      <Crumbs items={crumbs} />
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="font-display text-3xl font-semibold tracking-tight">{title}</h1>
          {subtitle ? <p className="mt-1 text-sm text-muted">{subtitle}</p> : null}
        </div>
        {actions}
      </div>
      {children}
    </div>
  );
}

export function State({ error, data }: { error: string; data: OrganizationDashboard | null }) {
  if (error) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;
  if (!data) return <Loading />;
  return null;
}

export function changeText(value: number | null) {
  if (value === null || Number.isNaN(value)) return "No earlier period to compare";
  const rounded = Math.round(value * 10) / 10;
  if (rounded === 0) return "Same as the previous month";
  return `${rounded > 0 ? "+" : ""}${rounded} points vs previous month`;
}

export function riskLabel(kind: string) {
  const names: Record<string, string> = {
    OverdueGoal: "Overdue goal",
    PastTarget: "Past target date",
    Blocked: "Blocked",
    MissingPlan: "Missing plan",
    StaleProgress: "No recent update",
  };
  return names[kind] ?? label(kind);
}

export const selectClass = "rounded-xl border border-line bg-white px-3 py-2 text-sm";
