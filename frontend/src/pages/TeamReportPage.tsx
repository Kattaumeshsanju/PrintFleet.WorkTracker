import { useEffect, useState } from "react";
import { Bar, BarChart, CartesianGrid, Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { Button, Loading, PageHeader, Panel, useToast } from "../components/ui";
import { api, downloadFile, errorText } from "../services/api";
import type { TeamReport } from "../types";
import { currentMonthValue, label, monthLabel, parseMonth } from "../utils/format";

const colors = ["#1d6b60", "#b7771f", "#2f5f98", "#a33b32", "#8a8175"];

export function TeamReportPage() {
  const toast = useToast();
  const [month, setMonth] = useState(currentMonthValue());
  const [includeSelf, setIncludeSelf] = useState(false);
  const [report, setReport] = useState<TeamReport | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    const { year, month: monthNumber } = parseMonth(month);
    api<TeamReport>(`/api/reports/team?year=${year}&month=${monthNumber}&includeSelf=${includeSelf}`)
      .then(setReport)
      .catch((err) => setError(errorText(err)));
  }, [month, includeSelf]);

  if (error) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;
  if (!report) return <Loading />;
  const file = `team-${report.year}-${String(report.month).padStart(2, "0")}`;

  return (
    <div className="space-y-4">
      <PageHeader title="Team Monthly Report" subtitle={monthLabel(report.year, report.month)} actions={
        <>
          <Button variant="ghost" onClick={() => downloadFile(`/api/reports/team/export?year=${report.year}&month=${report.month}&includeSelf=${includeSelf}&format=xlsx`, `${file}.xlsx`).catch((err) => toast(errorText(err), "error"))}>Excel</Button>
          <Button variant="ghost" onClick={() => downloadFile(`/api/reports/team/export?year=${report.year}&month=${report.month}&includeSelf=${includeSelf}&format=pdf`, `${file}.pdf`).catch((err) => toast(errorText(err), "error"))}>PDF</Button>
        </>
      } />
      <div className="flex flex-wrap items-center gap-3">
        <input className="rounded-xl border border-line bg-white px-3 py-2" type="month" value={month} onChange={(event) => setMonth(event.target.value)} />
        <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={includeSelf} onChange={(event) => setIncludeSelf(event.target.checked)} /> Include my work</label>
        <span className="text-sm text-muted">{report.completionPercent}% team completion · {report.actualHours}h actual</span>
      </div>
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
