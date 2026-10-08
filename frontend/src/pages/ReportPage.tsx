import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Bar, BarChart, CartesianGrid, Cell, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { useAuth } from "../auth/AuthContext";
import { Badge, Button, Loading, PageHeader, Panel, useToast } from "../components/ui";
import { api, downloadFile, errorText } from "../services/api";
import type { MonthlyReport, User } from "../types";
import { currentMonthValue, formatHours, label, monthLabel, parseMonth } from "../utils/format";

const colors = ["#1d6b60", "#b7771f", "#2f5f98", "#a33b32", "#8a8175"];

export function ReportPage() {
  const { user } = useAuth();
  const toast = useToast();
  const [params] = useSearchParams();
  const [month, setMonth] = useState(currentMonthValue());
  const [userId, setUserId] = useState(params.get("userId") ?? "");
  const [people, setPeople] = useState<User[]>([]);
  const [report, setReport] = useState<MonthlyReport | null>(null);
  const [error, setError] = useState("");
  const lead = user?.role === "TeamLead";

  useEffect(() => { if (lead) api<User[]>("/api/users").then(setPeople).catch(() => undefined); }, [lead]);

  useEffect(() => {
    const { year, month: monthNumber } = parseMonth(month);
    const query = new URLSearchParams({ year: String(year), month: String(monthNumber) });
    if (lead && userId) query.set("userId", userId);
    api<MonthlyReport>(`/api/reports/monthly?${query}`).then(setReport).catch((err) => setError(errorText(err)));
  }, [month, userId, lead]);

  if (error) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;
  if (!report) return <Loading />;
  const target = lead && userId ? userId : report.employee.id;
  const fileBase = `${report.employee.name}-${report.year}-${String(report.month).padStart(2, "0")}`;
  const hours = report.dailyHours.filter((day) => !day.isWeekend).map((day) => ({ day: day.date.slice(8), hours: Math.round(day.minutes / 6) / 10 }));

  return (
    <div className="space-y-4">
      <PageHeader title={report.employee.id === user?.id ? "My Monthly Report" : `${report.employee.name}'s report`} subtitle={monthLabel(report.year, report.month)} actions={
        <>
          <Button variant="ghost" onClick={() => downloadFile(`/api/reports/monthly/export?year=${report.year}&month=${report.month}&userId=${target}&format=xlsx`, `${fileBase}.xlsx`).catch((err) => toast(errorText(err), "error"))}>Excel</Button>
          <Button variant="ghost" onClick={() => downloadFile(`/api/reports/monthly/export?year=${report.year}&month=${report.month}&userId=${target}&format=pdf`, `${fileBase}.pdf`).catch((err) => toast(errorText(err), "error"))}>PDF</Button>
        </>
      } />
      <div className="flex flex-wrap gap-2">
        <input className="rounded-xl border border-line bg-white px-3 py-2" type="month" value={month} onChange={(event) => setMonth(event.target.value)} />
        {lead ? <select className="rounded-xl border border-line bg-white px-3 py-2" value={userId} onChange={(event) => setUserId(event.target.value)}><option value="">Myself</option>{people.filter((person) => person.id !== user?.id).map((person) => <option key={person.id} value={person.id}>{person.name}</option>)}</select> : null}
        <Badge value={report.sheetStatus} />
      </div>
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Panel><div className="text-xs text-muted uppercase">Planned / completed</div><div className="font-display text-2xl">{report.plannedTasks} / {report.completed}</div></Panel>
        <Panel><div className="text-xs text-muted uppercase">In progress / blocked</div><div className="font-display text-2xl">{report.inProgress} / {report.blocked}</div></Panel>
        <Panel><div className="text-xs text-muted uppercase">Planned vs actual</div><div className="font-display text-2xl">{formatHours(report.plannedHours)} / {formatHours(report.actualHours)}</div></Panel>
        <Panel><div className="text-xs text-muted uppercase">Average daily</div><div className="font-display text-2xl">{report.averageDailyHours}h</div><div className="text-xs text-muted">{report.completionPercent}% complete</div></Panel>
      </div>
      <div className="grid gap-4 xl:grid-cols-3">
        <Panel title="Daily hours"><div className="h-56"><ResponsiveContainer width="100%" height="100%"><BarChart data={hours}><CartesianGrid stroke="#eee6d8" vertical={false} /><XAxis dataKey="day" /><YAxis /><Tooltip /><Bar dataKey="hours" fill="#1d6b60" /></BarChart></ResponsiveContainer></div></Panel>
        <Panel title="Planned vs actual"><div className="h-56"><ResponsiveContainer width="100%" height="100%"><BarChart data={[{ name: "Planned", hours: report.plannedHours }, { name: "Actual", hours: report.actualHours }]}><XAxis dataKey="name" /><YAxis /><Tooltip /><Bar dataKey="hours" fill="#b7771f" /></BarChart></ResponsiveContainer></div></Panel>
        <Panel title="Status distribution"><div className="h-56"><ResponsiveContainer width="100%" height="100%"><PieChart><Pie data={report.statusDistribution.map((item) => ({ name: label(item.status), value: item.count }))} dataKey="value" nameKey="name" outerRadius={80}>{report.statusDistribution.map((item, index) => <Cell key={item.status} fill={colors[index % colors.length]} />)}</Pie><Tooltip /></PieChart></ResponsiveContainer></div></Panel>
      </div>
    </div>
  );
}
