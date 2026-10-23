import { useEffect, useState } from "react";
import { Bar, BarChart, CartesianGrid, Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { Button, Loading, PageHeader, Panel, Stat, useToast } from "../components/ui";
import { useAuth } from "../auth/AuthContext";
import { api, downloadFile, errorText } from "../services/api";
import type { Department, TeamReport } from "../types";
import { currentMonthValue, label, monthLabel, parseMonth } from "../utils/format";

const colors = ["#1d6b60", "#b7771f", "#2f5f98", "#a33b32", "#8a8175"];

export function TeamReportPage() {
  const { user } = useAuth();
  const admin = user?.role === "Admin";
  const toast = useToast();
  const [month, setMonth] = useState(currentMonthValue());
  const [includeSelf, setIncludeSelf] = useState(false);
  const [departmentId, setDepartmentId] = useState("");
  const [departments, setDepartments] = useState<Department[]>([]);
  const [report, setReport] = useState<TeamReport | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    const { year, month: monthNumber } = parseMonth(month);
    const department = departmentId ? `&departmentId=${departmentId}` : "";
    api<TeamReport>(`/api/reports/team?year=${year}&month=${monthNumber}&includeSelf=${includeSelf}${department}`)
      .then(setReport)
      .catch((err) => setError(errorText(err)));
  }, [month, includeSelf, departmentId]);

  useEffect(() => { if (admin) api<Department[]>("/api/departments").then(setDepartments).catch(() => undefined); }, [admin]);

  if (error) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;
  if (!report) return <Loading />;
  const file = `team-${report.year}-${String(report.month).padStart(2, "0")}`;

  return (
    <div className="space-y-4">
      <PageHeader title="Team Monthly Report" subtitle={monthLabel(report.year, report.month)} actions={
        <>
          <Button variant="ghost" onClick={() => downloadFile(`/api/reports/team/export?year=${report.year}&month=${report.month}&includeSelf=${includeSelf}${departmentId ? `&departmentId=${departmentId}` : ""}&format=xlsx`, `${file}.xlsx`).catch((err) => toast(errorText(err), "error"))}>Excel</Button>
          <Button variant="ghost" onClick={() => downloadFile(`/api/reports/team/export?year=${report.year}&month=${report.month}&includeSelf=${includeSelf}${departmentId ? `&departmentId=${departmentId}` : ""}&format=pdf`, `${file}.pdf`).catch((err) => toast(errorText(err), "error"))}>PDF</Button>
        </>
      } />
      <div className="flex flex-wrap items-center gap-3">
        <input className="rounded-xl border border-line bg-white px-3 py-2" type="month" value={month} onChange={(event) => setMonth(event.target.value)} />
        {admin ? <select className="rounded-xl border border-line bg-white px-3 py-2" value={departmentId} onChange={(event) => setDepartmentId(event.target.value)}><option value="">All departments</option>{departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}</select> : null}
        <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={includeSelf} onChange={(event) => setIncludeSelf(event.target.checked)} /> Include my work</label>
        <span className="text-sm text-muted">{report.completionPercent}% team completion · {report.actualHours}h actual</span>
      </div>
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Stat label="Plan completion" value={`${report.completionPercent}%`} hint={`${report.completedTasks} of ${report.plannedTasks} tasks`} />
        <Stat label="Hours attainment" value={`${report.hoursAttainment}%`} hint={`${report.actualHours}h logged of ${report.plannedHours}h planned`} />
        <Stat label="Utilization" value={`${report.utilizationPercent}%`} hint={`${report.expectedHoursPerDay}h expected on ${report.availableDays} available days`} />
        <Stat label="Update compliance" value={`${report.updateCompliance}%`} hint={`${report.missingDays} missing days · ${report.lowHourDays} under 6h · ${report.blockedTasks} blocked`} />
      </div>
      <Panel title="KPI by employee">
        <p className="mb-3 text-sm text-muted">Counted through today. Weekends are excluded. Holiday and Leave are not expected working days. Utilization is logged hours against {report.expectedHoursPerDay} hours a day. A low-hour day has some time logged, but under 6 hours.</p>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[980px] text-left text-sm">
            <thead className="text-xs tracking-wide text-muted uppercase"><tr>{["Employee", "Plan", "Attainment", "Utilization", "Updates", "Logged", "Missing", "Low", "Leave", "Holiday"].map((heading) => <th key={heading} className="py-2 pr-3">{heading}</th>)}</tr></thead>
            <tbody>
              {report.rows.map((row) => (
                <tr key={row.userId} className="border-t border-line">
                  <td className="py-2 pr-3 font-semibold">{row.name}</td>
                  <td className="pr-3">{row.completionPercent}%</td>
                  <td className="pr-3">{row.hoursAttainment}%</td>
                  <td className="pr-3">{row.utilizationPercent}%</td>
                  <td className="pr-3">{row.updateCompliance}%</td>
                  <td className="pr-3">{row.daysLogged}/{row.availableDays}</td>
                  <td className="pr-3">{row.missingDays}</td>
                  <td className="pr-3">{row.lowHourDays}</td>
                  <td className="pr-3">{row.leaveDays}</td>
                  <td>{row.holidayDays}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Panel>
      <div className="overflow-x-auto rounded-2xl border border-line bg-card">
        <table className="w-full min-w-[760px] text-left text-sm">
          <thead className="bg-[#f7f3ea] text-xs tracking-wide text-muted uppercase"><tr>{["Employee", "Planned", "Completed", "Hours", "Completion"].map((heading) => <th key={heading} className="px-3 py-2">{heading}</th>)}</tr></thead>
          <tbody>
            {report.rows.map((row) => (
              <tr key={row.userId} className="border-t border-line">
                <td className="px-3 py-2 font-semibold">{row.name}<div className="text-xs font-normal text-muted">{row.jobTitle}</div></td>
                <td>{row.planned}</td>
                <td>{row.completed}</td>
                <td className="font-mono">{row.actualHours}</td>
                <td>{row.completionPercent}%</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="grid gap-4 xl:grid-cols-3">
        <Panel title="Hours per employee"><div className="h-64"><ResponsiveContainer width="100%" height="100%"><BarChart data={report.rows}><CartesianGrid stroke="#eee6d8" vertical={false} /><XAxis dataKey="name" hide /><YAxis /><Tooltip /><Bar dataKey="actualHours" fill="#1d6b60" name="Actual hours" /></BarChart></ResponsiveContainer></div></Panel>
        <Panel title="Planned vs actual"><div className="h-64"><ResponsiveContainer width="100%" height="100%"><BarChart data={report.rows}><XAxis dataKey="name" hide /><YAxis /><Tooltip /><Legend /><Bar dataKey="plannedHours" fill="#d8c7a4" name="Planned" /><Bar dataKey="actualHours" fill="#b7771f" name="Actual" /></BarChart></ResponsiveContainer></div></Panel>
        <Panel title="Tasks by status"><div className="h-64"><ResponsiveContainer width="100%" height="100%"><PieChart><Pie data={report.statusDistribution.map((item) => ({ name: label(item.status), value: item.count }))} dataKey="value" nameKey="name" outerRadius={80}>{report.statusDistribution.map((item, index) => <Cell key={item.status} fill={colors[index % colors.length]} />)}</Pie><Tooltip /></PieChart></ResponsiveContainer></div></Panel>
      </div>
    </div>
  );
}
