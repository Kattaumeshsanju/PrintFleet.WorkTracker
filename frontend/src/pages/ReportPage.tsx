import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Bar, BarChart, CartesianGrid, Cell, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { useAuth } from "../auth/AuthContext";
import { Badge, Button, Field, Loading, PageHeader, Panel, inputClass, useToast } from "../components/ui";
import { api, downloadFile, errorText } from "../services/api";
import type { MonthlyReport, User } from "../types";
import { canManage, canViewOrganization, currentMonthValue, formatHours, goalCompletion, label, monthLabel, parseMonth } from "../utils/format";

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
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [rangeError, setRangeError] = useState("");
  const lead = canManage(user?.role) || canViewOrganization(user?.role);

  useEffect(() => { if (lead) api<User[]>("/api/users").then(setPeople).catch(() => undefined); }, [lead]);

  useEffect(() => {
    const { year, month: monthNumber } = parseMonth(month);
    const query = new URLSearchParams({ year: String(year), month: String(monthNumber) });
    if (lead && userId) query.set("userId", userId);
    api<MonthlyReport>(`/api/reports/monthly?${query}`).then(setReport).catch((err) => setError(errorText(err)));
  }, [month, userId, lead]);

  function exportRange(format: "pdf" | "xlsx") {
    if (!from || !to) {
      setRangeError("Choose a start date and an end date.");
      return;
    }
    if (from > to) {
      setRangeError("Start date must be on or before the end date.");
      return;
    }
    setRangeError("");
    const owner = lead && userId ? `&userId=${userId}` : "";
    const extension = format === "pdf" ? "pdf" : "xlsx";
    downloadFile(`/api/reports/range/export?from=${from}&to=${to}${owner}&format=${format}`, `${report?.employee.name ?? "report"}-${from}-to-${to}.${extension}`).catch((err) => toast(errorText(err), "error"));
  }

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
      <Panel title="Date range">
        <div className="flex flex-wrap items-end gap-3">
          <div className="w-44"><Field label="Start date"><input className={inputClass} type="date" value={from} onChange={(event) => { setFrom(event.target.value); setRangeError(""); }} /></Field></div>
          <div className="w-44"><Field label="End date"><input className={inputClass} type="date" value={to} onChange={(event) => { setTo(event.target.value); setRangeError(""); }} /></Field></div>
          <Button onClick={() => exportRange("pdf")}>Generate PDF</Button>
          <Button variant="secondary" onClick={() => exportRange("xlsx")}>Export Excel</Button>
          <Button variant="ghost" onClick={() => { setFrom(""); setTo(""); setRangeError(""); }}>Reset</Button>
        </div>
        {rangeError ? <div className="mt-3 text-sm text-rose-800">{rangeError}</div> : <div className="mt-3 text-sm text-muted">PDF and Excel use activities from the start date through the end date, including both days.</div>}
      </Panel>
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Panel><div className="text-xs text-muted uppercase">Planned / completed</div><div className="font-display text-2xl">{report.plannedTasks} / {report.completed}</div></Panel>
        <Panel><div className="text-xs text-muted uppercase">In progress / blocked</div><div className="font-display text-2xl">{report.inProgress} / {report.blocked}</div></Panel>
        <Panel><div className="text-xs text-muted uppercase">Planned vs actual</div><div className="font-display text-2xl">{formatHours(report.plannedHours)} / {formatHours(report.actualHours)}</div></Panel>
        <Panel><div className="text-xs text-muted uppercase">Average daily</div><div className="font-display text-2xl">{report.averageDailyHours}h</div><div className="text-xs text-muted">{goalCompletion(report.completionPercent, report.plannedTasks)} complete</div></Panel>
      </div>
      <div className="grid gap-4 xl:grid-cols-3">
        <Panel title="Daily hours"><div className="h-56"><ResponsiveContainer width="100%" height="100%"><BarChart data={hours}><CartesianGrid stroke="#eee6d8" vertical={false} /><XAxis dataKey="day" /><YAxis /><Tooltip /><Bar dataKey="hours" fill="#1d6b60" /></BarChart></ResponsiveContainer></div></Panel>
        <Panel title="Planned vs actual"><div className="h-56"><ResponsiveContainer width="100%" height="100%"><BarChart data={[{ name: "Planned", hours: report.plannedHours }, { name: "Actual", hours: report.actualHours }]}><XAxis dataKey="name" /><YAxis /><Tooltip /><Bar dataKey="hours" fill="#b7771f" /></BarChart></ResponsiveContainer></div></Panel>
        <Panel title="Status distribution"><div className="h-56"><ResponsiveContainer width="100%" height="100%"><PieChart><Pie data={report.statusDistribution.map((item) => ({ name: label(item.status), value: item.count }))} dataKey="value" nameKey="name" outerRadius={80}>{report.statusDistribution.map((item, index) => <Cell key={item.status} fill={colors[index % colors.length]} />)}</Pie><Tooltip /></PieChart></ResponsiveContainer></div></Panel>
      </div>
    </div>
  );
}
