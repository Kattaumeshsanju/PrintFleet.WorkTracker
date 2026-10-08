import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { MonthCalendar } from "../calendar/MonthCalendar";
import { Badge, Loading, PageHeader, Panel } from "../components/ui";
import { api, errorText } from "../services/api";
import type { EmployeeOverview } from "../types";
import { currentMonthValue, formatHours, formatMinutes, monthLabel, parseMonth, shortDate } from "../utils/format";

export function EmployeePage() {
  const { userId = "" } = useParams();
  const navigate = useNavigate();
  const [month, setMonth] = useState(currentMonthValue());
  const [data, setData] = useState<EmployeeOverview | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    const { year, month: monthNumber } = parseMonth(month);
    api<EmployeeOverview>(`/api/users/${userId}/overview?year=${year}&month=${monthNumber}`)
      .then(setData)
      .catch((err) => setError(errorText(err)));
  }, [userId, month]);

  if (error) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;
  if (!data) return <Loading />;
  const chart = data.hoursByDay.filter((day) => !day.isWeekend).map((day) => ({ day: day.date.slice(8), hours: Math.round(day.minutes / 6) / 10 }));

  return (
    <div className="space-y-4">
      <PageHeader title={data.user.name} subtitle={`${data.user.jobTitle ?? data.user.role} · ${monthLabel(data.year, data.month)}`} actions={
        <>
          <input className="rounded-xl border border-line bg-white px-3 py-2" type="month" value={month} onChange={(event) => setMonth(event.target.value)} />
          <Link className="rounded-xl border border-line bg-white px-3 py-2 text-sm font-semibold" to={`/monthly-plan?userId=${data.user.id}`}>Monthly plan</Link>
          <Link className="rounded-xl border border-line bg-white px-3 py-2 text-sm font-semibold" to={`/daily?userId=${data.user.id}`}>Daily timeline</Link>
        </>
      } />
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Panel><div className="text-xs text-muted uppercase">Planned / completed</div><div className="font-display text-2xl font-semibold">{data.completed} / {data.plannedTasks}</div></Panel>
        <Panel><div className="text-xs text-muted uppercase">In progress / blocked</div><div className="font-display text-2xl font-semibold">{data.inProgress} / {data.blocked}</div></Panel>
        <Panel><div className="text-xs text-muted uppercase">Planned hours</div><div className="font-display text-2xl font-semibold">{formatHours(data.plannedHours)}</div></Panel>
        <Panel><div className="text-xs text-muted uppercase">Actual hours</div><div className="font-display text-2xl font-semibold">{formatHours(data.actualHours)}</div></Panel>
      </div>
      <Panel title="Hours by day">
        <div className="h-56">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={chart}><CartesianGrid stroke="#eee6d8" vertical={false} /><XAxis dataKey="day" /><YAxis /><Tooltip /><Bar dataKey="hours" fill="#1d6b60" radius={4} /></BarChart>
          </ResponsiveContainer>
        </div>
      </Panel>
      <Panel title="Monthly tasks">
        <div className="space-y-2">
          {data.tasks.map((task) => (
            <div key={task.id} className="flex items-center justify-between gap-3 border-b border-line py-2 text-sm">
              <div><div className="font-semibold">{task.title}</div><div className="text-xs text-muted">{task.projectName} · {shortDate(task.plannedStartDate)}–{shortDate(task.plannedEndDate)}</div></div>
              <Badge value={task.status} />
            </div>
          ))}
        </div>
      </Panel>
      <div className="grid gap-4 lg:grid-cols-2">
        <Panel title="Completed work">{data.completedWork.length === 0 ? <div className="text-sm text-muted">Nothing completed yet.</div> : data.completedWork.map((task) => <div key={task.id} className="py-1 text-sm">{task.title}</div>)}</Panel>
        <Panel title="Blocked work">{data.blockedWork.length === 0 ? <div className="text-sm text-muted">No blocked tasks.</div> : data.blockedWork.map((task) => <div key={task.id} className="py-1 text-sm">{task.title}{task.remarks ? ` — ${task.remarks}` : ""}</div>)}</Panel>
      </div>
      <Panel title="Recent daily timeline">
        {data.recentEntries.slice(0, 8).map((entry) => (
          <div key={entry.id} className="grid grid-cols-[6rem_8rem_1fr] gap-2 border-b border-line py-2 text-sm">
            <div>{shortDate(entry.workDate)}</div>
            <div className="font-mono">{entry.startTime}–{entry.endTime}</div>
            <div>{entry.description} <span className="text-muted">({formatMinutes(entry.durationMinutes)})</span></div>
          </div>
        ))}
      </Panel>
      <Panel title="Calendar">
        <MonthCalendar days={data.hoursByDay} onPick={(value) => navigate(`/daily?userId=${data.user.id}&date=${value}`)} />
      </Panel>
      <Panel title="Remarks">
        {data.remarks.length === 0 ? <div className="text-sm text-muted">No remarks this month.</div> : <ul className="list-disc space-y-1 pl-5 text-sm">{data.remarks.map((remark) => <li key={remark}>{remark}</li>)}</ul>}
      </Panel>
    </div>
  );
}
