import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { MonthCalendar } from "../calendar/MonthCalendar";
import { useAuth } from "../auth/AuthContext";
import { WorkEntryModal } from "../components/WorkEntryModal";
import { Badge, Button, Loading, PageHeader, Panel, useToast } from "../components/ui";
import { DayTimeline } from "../timeline/DayTimeline";
import { api, downloadFile, errorText } from "../services/api";
import type { CalendarMonth, DailyEntry, DayView, MonthlySheet, User } from "../types";
import { canViewOrganization, formatMinutes, label, monthLabel, parseMonth, todayInput } from "../utils/format";

export function DailyPage() {
  const { user } = useAuth();
  const toast = useToast();
  const [params, setParams] = useSearchParams();
  const [date, setDate] = useState(params.get("date") ?? todayInput());
  const [calendarMonth, setCalendarMonth] = useState((params.get("date") ?? todayInput()).slice(0, 7));
  const [userId, setUserId] = useState(params.get("userId") ?? "");
  const [day, setDay] = useState<DayView | null>(null);
  const [sheet, setSheet] = useState<MonthlySheet | null>(null);
  const [calendar, setCalendar] = useState<CalendarMonth | null>(null);
  const [people, setPeople] = useState<User[]>([]);
  const [tab, setTab] = useState<"timeline" | "table">("timeline");
  const [editing, setEditing] = useState<DailyEntry | null>(null);
  const [open, setOpen] = useState(false);
  const [selected, setSelected] = useState<DailyEntry | null>(null);
  const [error, setError] = useState("");
  const admin = user?.role === "Admin";
  const reviewer = canViewOrganization(user?.role);
  const target = userId || user?.id || "";

  async function load(nextDate = date, nextUser = userId) {
    const owner = reviewer && nextUser ? nextUser : undefined;
    const query = new URLSearchParams({ date: nextDate });
    if (owner) query.set("userId", owner);
    const [planYear, planMonth] = nextDate.slice(0, 7).split("-").map(Number);
    const [year, monthNumber] = (calendarMonth || nextDate.slice(0, 7)).split("-").map(Number);
    const calendarQuery = new URLSearchParams({ year: String(year), month: String(monthNumber) });
    if (owner) calendarQuery.set("userId", owner);
    const [dayData, plan, monthData] = await Promise.all([
      api<DayView>(`/api/daily-work/day?${query}`),
      api<MonthlySheet>("/api/monthly-work", { method: "POST", body: JSON.stringify({ year: planYear, month: planMonth, userId: owner ?? null }) }),
      api<CalendarMonth>(`/api/daily-work/calendar?${calendarQuery}`),
    ]);
    setDay(dayData);
    setSheet(plan);
    setCalendar(monthData);
    setSelected((current) => dayData.entries.find((entry) => entry.id === current?.id) ?? null);
  }

  useEffect(() => {
    if (reviewer) api<User[]>("/api/users").then(setPeople).catch(() => undefined);
  }, [reviewer]);

  useEffect(() => {
    load().catch((err) => setError(errorText(err)));
  }, [date, userId, calendarMonth]);

  function shiftCalendar(delta: number) {
    const { year, month } = parseMonth(calendarMonth);
    const next = new Date(year, month - 1 + delta, 1);
    setCalendarMonth(`${next.getFullYear()}-${String(next.getMonth() + 1).padStart(2, "0")}`);
  }

  function changeDate(value: string) {
    setDate(value);
    setCalendarMonth(value.slice(0, 7));
    const next = new URLSearchParams(params);
    next.set("date", value);
    if (userId) next.set("userId", userId);
    setParams(next);
  }

  async function remove(entry: DailyEntry) {
    if (!window.confirm("Delete this work entry?")) return;
    try {
      await api(`/api/daily-work/${entry.id}`, { method: "DELETE" });
      toast("Work entry deleted.");
      setSelected(null);
      await load();
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  async function mark(dayType: string | null) {
    try {
      await api("/api/daily-work/day-mark", { method: "POST", body: JSON.stringify({ workDate: date, dayType }) });
      toast(dayType ? `Marked as ${dayType.toLowerCase()}.` : "Mark cleared.");
      await load();
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  async function submit() {
    try {
      await api("/api/daily-work/submit", { method: "POST", body: JSON.stringify({ workDate: date }) });
      toast("Daily update submitted.");
      await load();
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  async function reopen() {
    if (!day) return;
    try {
      await api("/api/daily-work/reopen", { method: "POST", body: JSON.stringify({ workDate: date, userId: day.userId }) });
      toast("Daily update reopened.");
      await load();
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  if (error) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;
  if (!day) return <Loading />;

  return (
    <div>
      <PageHeader title={day.userId === user?.id ? "My Daily Work" : `${day.userName}'s daily work`} subtitle="Record what you actually did today. Logging time does not complete a monthly goal. This timeline stays private." actions={
        <>
          <Button variant="ghost" onClick={() => downloadFile(`/api/reports/daily/export?date=${date}${target ? `&userId=${target}` : ""}&format=xlsx`, `${day.userName}-${date}.xlsx`).catch((err) => toast(errorText(err), "error"))}>Excel</Button>
          <Button variant="ghost" onClick={() => downloadFile(`/api/reports/daily/export?date=${date}${target ? `&userId=${target}` : ""}&format=pdf`, `${day.userName}-${date}.pdf`).catch((err) => toast(errorText(err), "error"))}>PDF</Button>
          <Button variant="ghost" onClick={() => {
            const [year, monthNumber] = date.slice(0, 7).split("-");
            const owner = reviewer && userId ? `&userId=${userId}` : "";
            downloadFile(`/api/reports/daily/month-export?year=${year}&month=${Number(monthNumber)}${owner}`, `${day.userName}-${year}-${monthNumber}-timeline.pdf`).catch((err) => toast(errorText(err), "error"));
          }}>Month PDF</Button>
          {day.canEdit ? <Button onClick={() => { setEditing(null); setOpen(true); }}>+ Add Work Entry</Button> : null}
        </>
      } />
      <div className="mb-4 flex flex-wrap items-end gap-3">
        <label className="text-sm font-semibold">Date
          <input className="mt-1 block rounded-xl border border-line bg-white px-3 py-2" type="date" value={date} onChange={(event) => changeDate(event.target.value)} />
        </label>
        {reviewer ? (
          <label className="text-sm font-semibold">Employee
            <select className="mt-1 block rounded-xl border border-line bg-white px-3 py-2" value={userId} onChange={(event) => setUserId(event.target.value)}>
              <option value="">Myself</option>
              {people.filter((person) => person.id !== user?.id).map((person) => <option key={person.id} value={person.id}>{person.name}</option>)}
            </select>
          </label>
        ) : null}
        <Badge value={day.dayType ?? day.submissionStatus} />
        {day.monthLocked ? <Badge value="Locked" /> : null}
        <div className="ml-auto font-display text-xl font-semibold">Daily total: {formatMinutes(day.totalMinutes)}</div>
      </div>
      <div className="mb-4">
        <Panel
          title={`Submitted dates · ${calendar?.userName ?? day.userName}`}
          action={
            <div className="flex items-center gap-2 text-sm font-semibold">
              <button type="button" className="rounded-lg border border-line px-2 py-1" onClick={() => shiftCalendar(-1)} aria-label="Previous month">←</button>
              <span>{monthLabel(calendar?.year ?? parseMonth(calendarMonth).year, calendar?.month ?? parseMonth(calendarMonth).month)}</span>
              <button type="button" className="rounded-lg border border-line px-2 py-1" onClick={() => shiftCalendar(1)} aria-label="Next month">→</button>
            </div>
          }
        >
          {calendar ? <MonthCalendar days={calendar.days} userName={calendar.userName} selected={date} onPick={changeDate} /> : <Loading />}
        </Panel>
      </div>
      <div className="mb-4 flex gap-2">
        {(["timeline", "table"] as const).map((item) => (
          <button key={item} className={`rounded-full px-4 py-1.5 text-sm font-semibold capitalize ${tab === item ? "bg-ink text-white" : "bg-card ring-1 ring-line"}`} onClick={() => setTab(item)}>{item}</button>
        ))}
      </div>
      {day.canEdit ? (
        <div className="mb-4 flex flex-wrap items-center gap-2">
          <span className="text-sm font-semibold">Mark this date</span>
          <Button variant={day.dayType === "Holiday" ? "primary" : "ghost"} onClick={() => mark(day.dayType === "Holiday" ? null : "Holiday")}>Holiday</Button>
          <Button variant={day.dayType === "Leave" ? "primary" : "ghost"} onClick={() => mark(day.dayType === "Leave" ? null : "Leave")}>Leave</Button>
        </div>
      ) : null}
      {day.dayType ? <div className={`mb-4 rounded-2xl px-4 py-3 text-sm ${day.dayType === "Holiday" ? "bg-amber-50 text-amber-950" : "bg-sky-50 text-sky-950"}`}>This date is marked as {day.dayType.toLowerCase()}.</div> : null}
      {tab === "timeline" ? <Panel><DayTimeline entries={day.entries} dayType={day.dayType} onSelect={setSelected} /></Panel> : null}
      {tab === "table" ? (
        <div className="overflow-x-auto rounded-2xl border border-line bg-card">
          <table className="w-full min-w-[860px] text-left text-sm">
            <thead className="bg-slate-50 text-xs tracking-wide text-muted uppercase"><tr>{["Start", "End", "Duration", "Monthly task", "Activity", "Status", "Remarks", ""].map((heading) => <th key={heading} className="px-3 py-2">{heading}</th>)}</tr></thead>
            <tbody>
              {day.entries.map((entry) => (
                <tr key={entry.id} className="border-t border-line">
                  <td className="px-3 py-2 font-mono">{entry.startTime}</td>
                  <td className="font-mono">{entry.endTime}</td>
                  <td className="font-mono">{formatMinutes(entry.durationMinutes)}</td>
                  <td>{entry.monthlyTaskTitle}</td>
                  <td>{entry.description}</td>
                  <td><Badge value={entry.status} /></td>
                  <td className="text-muted">{entry.remarks}</td>
                  <td className="px-3">{day.canEdit ? <button className="text-sm font-semibold text-pine" onClick={() => { setEditing(entry); setOpen(true); }}>Edit</button> : null}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}

      {selected ? (
        <Panel title="Activity">
          <div className="grid gap-2 text-sm sm:grid-cols-2">
            <div><span className="text-muted">Task</span><div className="font-semibold">{selected.monthlyTaskTitle}</div></div>
            <div><span className="text-muted">Project</span><div>{selected.projectName ?? "—"}</div></div>
            <div><span className="text-muted">Time</span><div className="font-mono">{selected.startTime}–{selected.endTime} · {formatMinutes(selected.durationMinutes)}</div></div>
            <div><span className="text-muted">Status</span><div><Badge value={selected.status} /></div></div>
            <div className="sm:col-span-2"><span className="text-muted">Description</span><div>{selected.description}</div></div>
            <div className="sm:col-span-2"><span className="text-muted">Remarks</span><div>{selected.remarks || "—"}</div></div>
          </div>
          {day.canEdit ? (
            <div className="mt-3 flex gap-2">
              <Button variant="ghost" onClick={() => { setEditing(selected); setOpen(true); }}>Edit</Button>
              <Button variant="danger" onClick={() => remove(selected)}>Delete</Button>
            </div>
          ) : null}
        </Panel>
      ) : null}

      <div className="mt-4 flex gap-2">
        {day.canEdit ? <Button variant="secondary" onClick={submit} disabled={day.entries.length === 0}>Submit Daily Update</Button> : null}
        {admin && day.submissionStatus === "Submitted" ? <Button variant="ghost" onClick={reopen}>Reopen update</Button> : null}
        {day.submissionStatus === "Submitted" ? <span className="self-center text-sm text-pine">Submitted ✓ {day.submittedAt ? new Date(day.submittedAt).toLocaleString() : ""}</span> : <span className="self-center text-sm text-muted">{label(day.submissionStatus)}</span>}
      </div>
      <WorkEntryModal open={open} date={date} tasks={sheet?.tasks ?? []} entries={day.entries} editing={editing} onClose={() => setOpen(false)} onSaved={() => { load().catch((err) => toast(errorText(err), "error")); }} />
    </div>
  );
}
