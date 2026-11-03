import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { api, downloadFile, errorText } from "../../services/api";
import type { Department, TeamReport } from "../../types";
import { formatHours, goalCompletion, hasOverview, monthLabel, parseMonth } from "../../utils/format";
import { Button } from "../../components/ui";
import { PeriodNote, Shell, State, selectClass, useExecutivePeriod, useOrganization } from "./shared";

export function ExecutiveReports() {
  const { user } = useAuth();
  const overview = hasOverview(user);
  const period = useExecutivePeriod();
  const { data, error } = useOrganization(period.month, { departmentId: period.departmentId }, overview);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [report, setReport] = useState<TeamReport | null>(null);
  const [reportError, setReportError] = useState("");

  useEffect(() => {
    api<Department[]>("/api/departments").then(setDepartments).catch(() => undefined);
  }, []);

  useEffect(() => {
    const { year, month } = parseMonth(period.month);
    const department = period.departmentId ? `&departmentId=${period.departmentId}` : "";
    api<TeamReport>(`/api/reports/team?year=${year}&month=${month}&includeSelf=false&organization=true${department}`)
      .then((value) => { setReport(value); setReportError(""); })
      .catch((err) => setReportError(errorText(err)));
  }, [period.month, period.departmentId]);

  if (overview) {
    const blocked = State({ error, data });
    if (blocked) return blocked;
    if (!data) return null;
  }

  const { year, month } = parseMonth(period.month);
  const department = period.departmentId ? `&departmentId=${period.departmentId}` : "";
  const exportPath = `/api/reports/team/export?year=${year}&month=${month}&includeSelf=false&organization=true${department}&format=xlsx`;

  return (
    <Shell
      title="Work Reports"
      subtitle="Monthly execution first. Authorized daily totals are separate and do not include private timeline text."
      crumbs={overview ? [{ label: "Executive Overview", to: period.href("/executive") }, { label: "Work Reports" }] : [{ label: "Work Reports" }]}
      actions={
        <div className="flex flex-wrap gap-2">
          <select className={selectClass} value={period.departmentId} onChange={(event) => period.setFilter("departmentId", event.target.value)} aria-label="Department">
            <option value="">All departments</option>
            {departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}
          </select>
          <input className={selectClass} type="month" value={period.month} onChange={(event) => period.setFilter("month", event.target.value)} aria-label="Reporting month" />
          <Button type="button" variant="ghost" onClick={() => downloadFile(exportPath, `organization-${period.month}.xlsx`).catch((err) => setReportError(errorText(err)))}>Excel</Button>
        </div>
      }
    >
      {data ? <PeriodNote data={data} /> : <p className="text-sm text-muted">Organization report. Department names stay attached to each result. This view does not open private daily timelines.</p>}
      {reportError ? <p className="text-sm text-rose-800">{reportError}</p> : null}
      {!report ? <p className="text-sm text-muted">Loading the report…</p> : (
        <>
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <div className="rounded-2xl border border-line bg-card p-4"><div className="text-xs text-muted uppercase">Plan completion</div><div className="mt-1 font-display text-2xl">{goalCompletion(report.completionPercent, report.plannedTasks)}</div><div className="text-xs text-muted">{monthLabel(report.year, report.month)}</div></div>
            <div className="rounded-2xl border border-line bg-card p-4"><div className="text-xs text-muted uppercase">Planned hours</div><div className="mt-1 font-display text-2xl">{formatHours(report.plannedHours)}</div></div>
            <div className="rounded-2xl border border-line bg-card p-4"><div className="text-xs text-muted uppercase">Logged hours</div><div className="mt-1 font-display text-2xl">{formatHours(report.actualHours)}</div><div className="text-xs text-muted">{report.plannedHours === 0 ? "No planned hours this month." : `${report.hoursAttainment}% of the plan`}</div></div>
            <div className="rounded-2xl border border-line bg-card p-4"><div className="text-xs text-muted uppercase">Blocked goals</div><div className="mt-1 font-display text-2xl">{report.blockedTasks}</div></div>
          </div>
          {report.includesDailyMetrics ? (
            <section className="rounded-2xl border border-line bg-card p-4">
              <h2 className="font-display text-lg font-semibold">Authorized daily completion</h2>
              <p className="mb-3 text-sm text-muted">Counts only. Individual daily descriptions stay on the work-detail page.</p>
              <div className="overflow-x-auto">
                <table className="w-full min-w-[640px] text-left text-sm">
                  <thead className="text-xs text-muted uppercase"><tr>{["Employee", "Days logged", "Missing days", "Low-hour days", ""].map((heading) => <th key={heading} className="px-3 py-2">{heading}</th>)}</tr></thead>
                  <tbody>
                    {report.rows.map((row) => (
                      <tr key={row.userId} className="border-t border-line">
                        <td className="px-3 py-2 font-semibold">{row.name}</td>
                        <td>{row.daysLogged}</td>
                        <td>{row.missingDays}</td>
                        <td>{row.lowHourDays}</td>
                        <td><Link className="font-semibold text-pine" to={`/daily?userId=${row.userId}`}>Work details</Link></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          ) : null}
        </>
      )}
    </Shell>
  );
}
