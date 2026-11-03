import { useEffect, useMemo, useState } from "react";
import { Badge, Button, Field, Loading, PageHeader, inputClass, useToast } from "../components/ui";
import { useAuth } from "../auth/AuthContext";
import { api, downloadFile, errorText } from "../services/api";
import type { Department } from "../types";
import { label } from "../utils/format";

type View = "entry" | "reports" | "holidays" | "settings";

type Mark = { date: string; status: string; remarks?: string | null; leaveType?: string | null; earnedDate?: string | null };
type Employee = {
  userId: string;
  name: string;
  employeeNumber?: string | null;
  present: number;
  leave: number;
  compOff: number;
  od: number;
  absent: number;
  unmarked: number;
  totalWorked: number;
  leaveFrom?: string | null;
  leaveTo?: string | null;
  odFrom?: string | null;
  odTo?: string | null;
  remarks?: string | null;
  marks: Mark[];
};
type Month = {
  departmentId: string;
  departmentName: string;
  year: number;
  month: number;
  registerStatus: string;
  canEdit: boolean;
  canReview: boolean;
  availableWorkingDays: number;
  holidayCount: number;
  activeEmployees: number;
  unmarkedWorkingDays: number;
  preparedOn: string;
  reviewReason?: string | null;
  dates: { date: string; working: boolean; holiday: boolean; holidayName?: string | null }[];
  employees: Employee[];
  warnings: string[];
};
type Report = {
  year: number;
  month: number;
  departments: {
    departmentId: string;
    departmentName: string;
    registerStatus: string;
    activeEmployees: number;
    availableWorkingDays: number;
    present: number;
    leave: number;
    compOff: number;
    od: number;
    absent: number;
    unmarked: number;
    completionPercent: number;
  }[];
};
type Holiday = { id: string; name: string; holidayDate: string; holidayType: string; departmentId?: string | null; departmentName?: string | null };
type Calendar = { departmentId: string; departmentName: string; weeklyOffs: number[]; alternateSaturday: string };
type NumberRow = { userId: string; name: string; departmentName?: string | null; employeeNumber?: string | null };
type Correction = { id: string; userId: string; userName: string; date: string; requestedStatus: string; reason: string; status: string };

const statuses = ["Present", "Leave", "CompOff", "OD", "Absent", "WeeklyOff", "Holiday", "NotMarked"];
const dayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
const saturdayRules = ["None", "AllOff", "SecondAndFourth", "FirstAndThird"];

function monthValue(year: number, month: number) {
  return `${year}-${String(month).padStart(2, "0")}`;
}

function statusOf(person: Employee, date: string) {
  return person.marks.find((mark) => mark.date === date)?.status ?? "NotMarked";
}

export function AttendancePage({ view }: { view: View }) {
  const { user } = useAuth();
  const toast = useToast();
  const today = new Date();
  const [period, setPeriod] = useState(monthValue(today.getFullYear(), today.getMonth() + 1));
  const [departmentId, setDepartmentId] = useState("");
  const [departments, setDepartments] = useState<Department[]>([]);
  const [month, setMonth] = useState<Month | null>(null);
  const [date, setDate] = useState(today.toISOString().slice(0, 10));
  const [panel, setPanel] = useState<"daily" | "register">("daily");
  const [employeeId, setEmployeeId] = useState("");
  const [report, setReport] = useState<Report | null>(null);
  const [holidays, setHolidays] = useState<Holiday[]>([]);
  const [calendars, setCalendars] = useState<Calendar[]>([]);
  const [numbers, setNumbers] = useState<NumberRow[]>([]);
  const [corrections, setCorrections] = useState<Correction[]>([]);
  const [reason, setReason] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [holidayName, setHolidayName] = useState("");
  const [holidayDate, setHolidayDate] = useState(today.toISOString().slice(0, 10));
  const [holidayType, setHolidayType] = useState("Company");
  const [holidayDepartment, setHolidayDepartment] = useState("");
  const role = user?.role;
  const canChooseDepartment = role === "Admin" || role === "CEO";
  const [year, monthNumber] = period.split("-").map(Number);

  useEffect(() => {
    if (!canChooseDepartment) return;
    api<Department[]>("/api/departments").then(setDepartments).catch(() => undefined);
  }, [canChooseDepartment]);

  useEffect(() => {
    if (view === "reports") {
      api<Report>(`/api/attendance/reports?year=${year}&month=${monthNumber}`).then((data) => { setReport(data); setError(""); }).catch((err) => setError(errorText(err)));
      return;
    }
    if (view === "holidays" || view === "settings") {
      api<Holiday[]>(`/api/attendance/holidays?year=${year}`).then(setHolidays).catch((err) => setError(errorText(err)));
      if (view === "settings") {
        api<Calendar[]>("/api/attendance/calendars").then(setCalendars).catch((err) => setError(errorText(err)));
        api<NumberRow[]>("/api/attendance/employees").then(setNumbers).catch((err) => setError(errorText(err)));
      }
      return;
    }
    const query = new URLSearchParams({ year: String(year), month: String(monthNumber) });
    if (departmentId) query.set("departmentId", departmentId);
    api<Month>(`/api/attendance/month?${query}`).then((data) => {
      setMonth(data);
      setError("");
      if (!data.dates.some((item) => item.date === date)) setDate(data.dates[0]?.date ?? date);
    }).catch((err) => setError(errorText(err)));
    api<Correction[]>(`/api/attendance/corrections${departmentId ? `?departmentId=${departmentId}` : ""}`).then(setCorrections).catch(() => setCorrections([]));
  }, [view, year, monthNumber, departmentId]);

  const visible = useMemo(() => {
    if (!month) return [];
    return employeeId ? month.employees.filter((person) => person.userId === employeeId) : month.employees;
  }, [month, employeeId]);

  const completion = month && month.employees.length > 0 && month.availableWorkingDays > 0
    ? Math.round((1 - month.unmarkedWorkingDays / (month.employees.length * month.availableWorkingDays)) * 1000) / 10
    : 100;

  async function refresh() {
    const query = new URLSearchParams({ year: String(year), month: String(monthNumber) });
    if (departmentId || month?.departmentId) query.set("departmentId", departmentId || month?.departmentId || "");
    const data = await api<Month>(`/api/attendance/month?${query}`);
    setMonth(data);
    const correctionsQuery = departmentId || data.departmentId;
    setCorrections(await api<Correction[]>(`/api/attendance/corrections?departmentId=${correctionsQuery}`));
  }

  async function run(action: () => Promise<void>) {
    setBusy(true);
    try {
      await action();
      setError("");
    } catch (err) {
      toast(errorText(err), "error");
    } finally {
      setBusy(false);
    }
  }

  async function saveDay(userId: string, status: string, remarks?: string) {
    await run(async () => {
      const data = await api<Month>("/api/attendance/day", {
        method: "PUT",
        body: JSON.stringify({ userId, date, status, remarks }),
      });
      setMonth(data);
      toast(status === "NotMarked" ? "Attendance mark cleared." : "Attendance saved.");
    });
  }

  function titles() {
    if (view === "reports") return role === "CEO" ? ["Organization Attendance Overview", "Approved and in-progress attendance, read only."] : ["Attendance Reports", "Department totals for the selected month."];
    if (view === "holidays") return ["Holiday Calendar", "Company holidays used by attendance calculations."];
    if (view === "settings") return ["Attendance Settings", "Work calendars and employee numbers."];
    if (role === "TeamMember") return ["My Attendance", "Your recorded attendance. Unmarked days are not absences."];
    if (role === "CEO") return ["Department Attendance", "Read-only attendance for the selected department."];
    if (role === "TeamLead") return ["My Team Attendance", month?.departmentName ?? "Your department"];
    return ["Organization Attendance", "Record and review attendance for any department."];
  }
  const [title, subtitle] = titles();

  if (error && !month && view === "entry") return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;

  return (
    <div>
      <PageHeader
        title={title}
        subtitle={subtitle}
        actions={view === "entry" && role !== "TeamMember" && month ? (
          <Button variant="ghost" disabled={busy} onClick={() => downloadFile(`/api/attendance/export?year=${month.year}&month=${month.month}&departmentId=${month.departmentId}`, `${month.departmentName}-${month.year}-${String(month.month).padStart(2, "0")}-attendance.xlsx`).catch((err) => toast(errorText(err), "error"))}>Export Excel</Button>
        ) : null}
      />
      <div className="mb-4 flex flex-wrap gap-2">
        <input className={inputClass} style={{ width: 180 }} type="month" value={period} onChange={(event) => setPeriod(event.target.value)} aria-label="Month" />
        {canChooseDepartment && view !== "settings" && view !== "holidays" ? (
          <select className={inputClass} style={{ width: 240 }} value={departmentId} onChange={(event) => setDepartmentId(event.target.value)} aria-label="Department">
            <option value="">{view === "reports" ? "All departments" : "First department"}</option>
            {departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}
          </select>
        ) : null}
      </div>

      {view === "reports" ? (
        error ? <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div> : report ? <ReportTable report={{ ...report, departments: departmentId ? report.departments.filter((row) => row.departmentId === departmentId) : report.departments }} /> : <Loading />
      ) : null}

      {view === "holidays" ? (
        <HolidayPanel
          holidays={holidays}
          departments={departments}
          name={holidayName}
          date={holidayDate}
          type={holidayType}
          departmentId={holidayDepartment}
          onName={setHolidayName}
          onDate={setHolidayDate}
          onType={setHolidayType}
          onDepartment={setHolidayDepartment}
          onAdd={() => run(async () => {
            await api("/api/attendance/holidays", { method: "POST", body: JSON.stringify({ name: holidayName, holidayDate, holidayType, departmentId: holidayDepartment || null }) });
            setHolidays(await api<Holiday[]>(`/api/attendance/holidays?year=${year}`));
            setHolidayName("");
            toast("Holiday added.");
          })}
          onDelete={(id) => run(async () => {
            await api(`/api/attendance/holidays/${id}`, { method: "DELETE" });
            setHolidays((rows) => rows.filter((row) => row.id !== id));
            toast("Holiday removed.");
          })}
        />
      ) : null}

      {view === "settings" ? (
        <SettingsPanel calendars={calendars} numbers={numbers} onCalendar={(calendar) => run(async () => {
          await api(`/api/attendance/calendars/${calendar.departmentId}`, { method: "PUT", body: JSON.stringify({ weeklyOffs: calendar.weeklyOffs, alternateSaturday: calendar.alternateSaturday }) });
          toast(`${calendar.departmentName} calendar saved.`);
        })} onNumber={(row) => run(async () => {
          await api(`/api/attendance/employees/${row.userId}/number`, { method: "PUT", body: JSON.stringify({ employeeNumber: row.employeeNumber }) });
          toast("Employee number saved.");
        })} />
      ) : null}

      {view === "entry" && month ? (
        <>
          <div className="mb-4 grid gap-3 md:grid-cols-4">
            <Stat label="Department" value={month.departmentName} />
            <Stat label="Working days" value={`${month.availableWorkingDays}`} hint={`${month.holidayCount} holidays`} />
            <Stat label="Active employees" value={`${month.activeEmployees}`} />
            <Stat label="Completion" value={`${completion}%`} hint={`${month.unmarkedWorkingDays} unmarked working days`} />
          </div>
          <div className="mb-4 flex flex-wrap items-center gap-2">
            <Badge value={month.registerStatus} />
            <span className="text-sm text-muted">Prepared {month.preparedOn}</span>
            {month.reviewReason ? <span className="text-sm text-muted">Reason: {month.reviewReason}</span> : null}
          </div>
          {month.warnings.length > 0 ? (
            <div className="mb-4 rounded-2xl bg-amber-50 px-4 py-3 text-sm text-amber-950">
              <div className="mb-1 font-semibold">Checks</div>
              <ul className="list-disc pl-5">{month.warnings.slice(0, 12).map((warning) => <li key={warning}>{warning}</li>)}</ul>
            </div>
          ) : null}
          <div className="mb-4 flex gap-2">
            <Button variant={panel === "daily" ? "secondary" : "ghost"} onClick={() => setPanel("daily")}>{role === "TeamMember" ? "My days" : "Daily entry"}</Button>
            <Button variant={panel === "register" ? "secondary" : "ghost"} onClick={() => setPanel("register")}>{role === "TeamMember" ? "My history" : "Monthly register"}</Button>
          </div>
          {panel === "daily" ? (
            <DailyPanel
              month={month}
              date={date}
              people={visible}
              employeeId={employeeId}
              busy={busy}
              member={role === "TeamMember"}
              onDate={setDate}
              onEmployee={setEmployeeId}
              onStatus={saveDay}
              onPresent={() => run(async () => {
                setMonth(await api<Month>("/api/attendance/present", { method: "POST", body: JSON.stringify({ date, departmentId: month.departmentId }) }));
                toast("Unmarked working-day rows were marked Present.");
              })}
              onRange={(body) => run(async () => {
                setMonth(await api<Month>("/api/attendance/ranges", { method: "POST", body: JSON.stringify(body) }));
                toast("Range saved on working days only.");
              })}
              onCorrection={(body) => run(async () => {
                await api("/api/attendance/corrections", { method: "POST", body: JSON.stringify(body) });
                toast("Correction request sent.");
                await refresh();
              })}
            />
          ) : (
            <RegisterTable month={month} people={visible} />
          )}
          {role !== "CEO" && role !== "TeamMember" ? (
            <div className="mt-4 flex flex-wrap items-end gap-2">
              <Field label="Review note">
                <input className={inputClass} value={reason} onChange={(event) => setReason(event.target.value)} />
              </Field>
              {month.registerStatus === "Draft" ? <Button disabled={busy} onClick={() => run(async () => { setMonth(await api<Month>("/api/attendance/submit", { method: "POST", body: JSON.stringify({ departmentId: month.departmentId, year: month.year, month: month.month, reason }) })); toast("Register submitted."); })}>Submit register</Button> : null}
              {month.canReview && month.registerStatus === "Submitted" ? (
                <>
                  <Button disabled={busy} onClick={() => run(async () => { setMonth(await api<Month>("/api/attendance/approve", { method: "POST", body: JSON.stringify({ departmentId: month.departmentId, year: month.year, month: month.month, reason }) })); toast("Register approved."); })}>Approve</Button>
                  <Button variant="danger" disabled={busy} onClick={() => run(async () => { setMonth(await api<Month>("/api/attendance/reject", { method: "POST", body: JSON.stringify({ departmentId: month.departmentId, year: month.year, month: month.month, reason }) })); toast("Register returned for correction."); })}>Return</Button>
                </>
              ) : null}
              {month.canReview && month.registerStatus === "Approved" ? (
                <Button variant="danger" disabled={busy || !reason.trim()} onClick={() => run(async () => { setMonth(await api<Month>("/api/attendance/reopen", { method: "POST", body: JSON.stringify({ departmentId: month.departmentId, year: month.year, month: month.month, reason }) })); toast("Register reopened."); })}>Reopen</Button>
              ) : null}
            </div>
          ) : null}
          {corrections.length > 0 && role !== "CEO" ? (
            <div className="mt-6">
              <h2 className="mb-2 text-lg font-semibold">Correction requests</h2>
              <ul className="space-y-2">
                {corrections.map((item) => (
                  <li key={item.id} className="flex flex-wrap items-center justify-between gap-2 rounded-2xl border border-line bg-white px-4 py-3 text-sm">
                    <span>{item.userName} · {item.date} · {label(item.requestedStatus)} · {item.reason}</span>
                    {role !== "TeamMember" && item.userId !== user?.id ? (
                      <span className="flex gap-2">
                        <Button disabled={busy} onClick={() => run(async () => { await api(`/api/attendance/corrections/${item.id}/decide?approve=true&reason=${encodeURIComponent(reason)}`, { method: "POST" }); toast("Correction approved."); await refresh(); })}>Approve</Button>
                        <Button variant="ghost" disabled={busy} onClick={() => run(async () => { await api(`/api/attendance/corrections/${item.id}/decide?approve=false&reason=${encodeURIComponent(reason)}`, { method: "POST" }); toast("Correction rejected."); await refresh(); })}>Reject</Button>
                      </span>
                    ) : <Badge value={item.status} />}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </>
      ) : null}
      {view === "entry" && !month && !error ? <Loading /> : null}
    </div>
  );
}

function Stat({ label: text, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="rounded-2xl border border-line bg-white px-4 py-3">
      <div className="text-xs font-semibold uppercase tracking-wide text-muted">{text}</div>
      <div className="text-xl font-semibold">{value}</div>
      {hint ? <div className="text-xs text-muted">{hint}</div> : null}
    </div>
  );
}

function DailyPanel({ month, date, people, employeeId, busy, member, onDate, onEmployee, onStatus, onPresent, onRange, onCorrection }: {
  month: Month;
  date: string;
  people: Employee[];
  employeeId: string;
  busy: boolean;
  member: boolean;
  onDate: (date: string) => void;
  onEmployee: (id: string) => void;
  onStatus: (userId: string, status: string, remarks?: string) => void;
  onPresent: () => void;
  onRange: (body: { userId: string; kind: string; startDate: string; endDate: string; leaveType?: string; earnedDate?: string; remarks?: string }) => void;
  onCorrection: (body: { date: string; requestedStatus: string; reason: string }) => void;
}) {
  const [remarks, setRemarks] = useState("");
  const [kind, setKind] = useState("Leave");
  const [start, setStart] = useState(date);
  const [end, setEnd] = useState(date);
  const [leaveType, setLeaveType] = useState("");
  const [earned, setEarned] = useState("");
  const [correction, setCorrection] = useState("Present");
  const [correctionReason, setCorrectionReason] = useState("");
  const selected = month.dates.find((item) => item.date === date);
  return (
    <div>
      <div className="mb-3 flex gap-1 overflow-x-auto pb-2">
        {month.dates.map((item) => {
          const missing = item.working && month.employees.some((person) => statusOf(person, item.date) === "NotMarked");
          return (
            <button key={item.date} type="button" onClick={() => onDate(item.date)} className={`min-w-12 rounded-xl border px-2 py-1 text-xs ${item.date === date ? "border-ink bg-ink text-white" : item.holiday || !item.working ? "border-line bg-stone-100 text-stone-500" : missing ? "border-amber-300 bg-amber-50" : "border-emerald-200 bg-emerald-50"}`}>
              <div>{item.date.slice(8)}</div>
              <div>{dayNames[new Date(`${item.date}T00:00:00`).getDay()]}</div>
            </button>
          );
        })}
      </div>
      <div className="mb-3 flex flex-wrap items-center gap-2">
        <strong>{date}</strong>
        {selected?.holidayName ? <span className="text-sm text-muted">{selected.holidayName}</span> : null}
        {!member ? (
          <select className={inputClass} style={{ width: 220 }} value={employeeId} onChange={(event) => onEmployee(event.target.value)} aria-label="Employee">
            <option value="">All employees</option>
            {month.employees.map((person) => <option key={person.userId} value={person.userId}>{person.name}</option>)}
          </select>
        ) : null}
        {month.canEdit && selected?.working ? <Button variant="secondary" disabled={busy} onClick={onPresent}>Mark unmarked Present</Button> : null}
      </div>
      <div className="overflow-x-auto rounded-2xl border border-line bg-white">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase tracking-wide text-muted">
            <tr>
              <th className="px-3 py-2">Employee number</th>
              <th className="px-3 py-2">Employee name</th>
              <th className="px-3 py-2">Attendance status</th>
              <th className="px-3 py-2">Remarks</th>
              {month.canEdit || member ? <th className="px-3 py-2">Action</th> : null}
            </tr>
          </thead>
          <tbody>
            {people.map((person) => {
              const status = statusOf(person, date);
              const mark = person.marks.find((item) => item.date === date);
              return (
                <tr key={person.userId} className="border-t border-line">
                  <td className="px-3 py-2">{person.employeeNumber || "Missing"}</td>
                  <td className="px-3 py-2 font-semibold">{person.name}</td>
                  <td className="px-3 py-2">{label(status)}</td>
                  <td className="px-3 py-2">{mark?.remarks || "—"}</td>
                  {month.canEdit ? (
                    <td className="px-3 py-2">
                      <div className="flex flex-wrap gap-1">
                        <Button variant="ghost" disabled={busy} onClick={() => onStatus(person.userId, "Present", remarks)}>Present</Button>
                        <Button variant="ghost" disabled={busy} onClick={() => onStatus(person.userId, "Leave", remarks)}>Leave</Button>
                        <Button variant="ghost" disabled={busy} onClick={() => onStatus(person.userId, "OD", remarks)}>OD</Button>
                        <Button variant="ghost" disabled={busy} onClick={() => onStatus(person.userId, "CompOff", remarks)}>C-off</Button>
                        <select className="rounded-lg border border-line px-2 py-1" aria-label={`Status for ${person.name}`} value={status} onChange={(event) => onStatus(person.userId, event.target.value, remarks)}>
                          {statuses.map((item) => <option key={item} value={item}>{label(item)}</option>)}
                        </select>
                      </div>
                    </td>
                  ) : null}
                  {member ? (
                    <td className="px-3 py-2">
                      <Button variant="ghost" disabled={busy || !correctionReason.trim()} onClick={() => onCorrection({ date, requestedStatus: correction, reason: correctionReason })}>Request correction</Button>
                    </td>
                  ) : null}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      {month.canEdit ? (
        <div className="mt-4 grid gap-3 rounded-2xl border border-line bg-white p-4 md:grid-cols-6">
          <Field label="Remarks"><input className={inputClass} value={remarks} onChange={(event) => setRemarks(event.target.value)} /></Field>
          <Field label="Employee">
            <select className={inputClass} value={employeeId} onChange={(event) => onEmployee(event.target.value)}>
              <option value="">Select</option>
              {month.employees.map((person) => <option key={person.userId} value={person.userId}>{person.name}</option>)}
            </select>
          </Field>
          <Field label="Range">
            <select className={inputClass} value={kind} onChange={(event) => setKind(event.target.value)}>
              <option value="Leave">Leave</option>
              <option value="CompOff">Compensatory off</option>
              <option value="OD">Outstation duty</option>
            </select>
          </Field>
          <Field label="From"><input className={inputClass} type="date" value={start} onChange={(event) => setStart(event.target.value)} /></Field>
          <Field label="To"><input className={inputClass} type="date" value={end} onChange={(event) => setEnd(event.target.value)} /></Field>
          <div className="flex items-end">
            <Button disabled={busy || !employeeId} onClick={() => onRange({ userId: employeeId, kind, startDate: start, endDate: end, leaveType, earnedDate: earned || undefined, remarks })}>Apply range</Button>
          </div>
          {kind === "Leave" ? <Field label="Leave type"><input className={inputClass} value={leaveType} onChange={(event) => setLeaveType(event.target.value)} /></Field> : null}
          {kind === "CompOff" ? <Field label="Date worked" hint="The day that earned this compensatory off."><input className={inputClass} type="date" value={earned} onChange={(event) => setEarned(event.target.value)} /></Field> : null}
        </div>
      ) : null}
      {member ? (
        <div className="mt-4 grid gap-3 md:grid-cols-3">
          <Field label="Requested status">
            <select className={inputClass} value={correction} onChange={(event) => setCorrection(event.target.value)}>
              {statuses.filter((item) => item !== "NotMarked").map((item) => <option key={item} value={item}>{label(item)}</option>)}
            </select>
          </Field>
          <Field label="Reason"><input className={inputClass} value={correctionReason} onChange={(event) => setCorrectionReason(event.target.value)} /></Field>
        </div>
      ) : null}
    </div>
  );
}

function RegisterTable({ month, people }: { month: Month; people: Employee[] }) {
  return (
    <div className="overflow-x-auto rounded-2xl border border-line bg-white">
      <table className="w-full min-w-[1100px] text-left text-sm">
        <thead className="bg-slate-50 text-xs uppercase tracking-wide text-muted">
          <tr>
            {["SL No.", "Employee number", "Employee name", "Present days", "Leave / C-off from", "Leave / C-off to", "Leave days", "OD from", "OD to", "Total working days", "Remarks / C-off in lieu"].map((heading) => <th key={heading} className="px-3 py-2">{heading}</th>)}
          </tr>
        </thead>
        <tbody>
          {people.map((person, index) => (
            <tr key={person.userId} className="border-t border-line">
              <td className="px-3 py-2">{index + 1}</td>
              <td className="px-3 py-2">{person.employeeNumber || "Missing"}</td>
              <td className="px-3 py-2 font-semibold">{person.name}</td>
              <td className="px-3 py-2">{person.present}</td>
              <td className="px-3 py-2">{person.leaveFrom || "—"}</td>
              <td className="px-3 py-2">{person.leaveTo || "—"}</td>
              <td className="px-3 py-2">{person.leave + person.compOff}</td>
              <td className="px-3 py-2">{person.odFrom || "—"}</td>
              <td className="px-3 py-2">{person.odTo || "—"}</td>
              <td className="px-3 py-2">{person.totalWorked}</td>
              <td className="px-3 py-2">{person.remarks || "—"}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="px-3 py-2 text-xs text-muted">Total working days = present days + outstation duty days. Available working days this month: {month.availableWorkingDays}. Weekly offs and holidays are excluded. Compensatory off is not present.</p>
    </div>
  );
}

function ReportTable({ report }: { report: Report }) {
  return (
    <div className="overflow-x-auto rounded-2xl border border-line bg-white">
      <table className="w-full text-left text-sm">
        <thead className="bg-slate-50 text-xs uppercase tracking-wide text-muted">
          <tr>
            {["Department", "Status", "Employees", "Present", "Leave", "C-off", "OD", "Absent", "Unmarked", "Completion"].map((heading) => <th key={heading} className="px-3 py-2">{heading}</th>)}
          </tr>
        </thead>
        <tbody>
          {report.departments.map((row) => (
            <tr key={row.departmentId} className="border-t border-line">
              <td className="px-3 py-2 font-semibold">{row.departmentName}</td>
              <td className="px-3 py-2"><Badge value={row.registerStatus} /></td>
              <td className="px-3 py-2">{row.activeEmployees}</td>
              <td className="px-3 py-2">{row.present}</td>
              <td className="px-3 py-2">{row.leave}</td>
              <td className="px-3 py-2">{row.compOff}</td>
              <td className="px-3 py-2">{row.od}</td>
              <td className="px-3 py-2">{row.absent}</td>
              <td className="px-3 py-2">{row.unmarked}</td>
              <td className="px-3 py-2">{row.completionPercent}%</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function HolidayPanel(props: {
  holidays: Holiday[];
  departments: Department[];
  name: string;
  date: string;
  type: string;
  departmentId: string;
  onName: (value: string) => void;
  onDate: (value: string) => void;
  onType: (value: string) => void;
  onDepartment: (value: string) => void;
  onAdd: () => void;
  onDelete: (id: string) => void;
}) {
  return (
    <div>
      <div className="mb-4 grid gap-3 rounded-2xl border border-line bg-white p-4 md:grid-cols-5">
        <Field label="Holiday"><input className={inputClass} value={props.name} onChange={(event) => props.onName(event.target.value)} /></Field>
        <Field label="Date"><input className={inputClass} type="date" value={props.date} onChange={(event) => props.onDate(event.target.value)} /></Field>
        <Field label="Type"><input className={inputClass} value={props.type} onChange={(event) => props.onType(event.target.value)} /></Field>
        <Field label="Departments">
          <select className={inputClass} value={props.departmentId} onChange={(event) => props.onDepartment(event.target.value)}>
            <option value="">All departments</option>
            {props.departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}
          </select>
        </Field>
        <div className="flex items-end"><Button onClick={props.onAdd}>Add holiday</Button></div>
      </div>
      <ul className="space-y-2">
        {props.holidays.map((holiday) => (
          <li key={holiday.id} className="flex items-center justify-between rounded-2xl border border-line bg-white px-4 py-3 text-sm">
            <span>{holiday.holidayDate} · {holiday.name} · {holiday.holidayType} · {holiday.departmentName || "All departments"}</span>
            <Button variant="ghost" onClick={() => props.onDelete(holiday.id)}>Remove</Button>
          </li>
        ))}
      </ul>
    </div>
  );
}

function SettingsPanel({ calendars, numbers, onCalendar, onNumber }: {
  calendars: Calendar[];
  numbers: NumberRow[];
  onCalendar: (calendar: Calendar) => void;
  onNumber: (row: NumberRow) => void;
}) {
  const [rows, setRows] = useState(calendars);
  const [people, setPeople] = useState(numbers);
  useEffect(() => setRows(calendars), [calendars]);
  useEffect(() => setPeople(numbers), [numbers]);
  return (
    <div className="space-y-6">
      <section>
        <h2 className="mb-2 text-lg font-semibold">Department work calendars</h2>
        <div className="space-y-3">
          {rows.map((calendar) => (
            <div key={calendar.departmentId} className="rounded-2xl border border-line bg-white p-4">
              <div className="mb-2 font-semibold">{calendar.departmentName}</div>
              <div className="mb-2 flex flex-wrap gap-2">
                {dayNames.map((name, index) => (
                  <label key={name} className="flex items-center gap-1 text-sm">
                    <input type="checkbox" checked={calendar.weeklyOffs.includes(index)} onChange={(event) => {
                      const weeklyOffs = event.target.checked ? [...calendar.weeklyOffs, index] : calendar.weeklyOffs.filter((day) => day !== index);
                      setRows((current) => current.map((item) => item.departmentId === calendar.departmentId ? { ...item, weeklyOffs } : item));
                    }} />
                    {name} off
                  </label>
                ))}
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <select className={inputClass} style={{ width: 260 }} value={calendar.alternateSaturday} onChange={(event) => setRows((current) => current.map((item) => item.departmentId === calendar.departmentId ? { ...item, alternateSaturday: event.target.value } : item))} aria-label={`${calendar.departmentName} Saturday rule`}>
                  {saturdayRules.map((rule) => <option key={rule} value={rule}>{label(rule)}</option>)}
                </select>
                <Button onClick={() => onCalendar(rows.find((item) => item.departmentId === calendar.departmentId) ?? calendar)}>Save calendar</Button>
              </div>
            </div>
          ))}
        </div>
      </section>
      <section>
        <h2 className="mb-2 text-lg font-semibold">Employee numbers</h2>
        <p className="mb-2 text-sm text-muted">Existing people stay valid until a number is entered. Numbers are not generated automatically.</p>
        <div className="space-y-2">
          {people.map((person) => (
            <div key={person.userId} className="flex flex-wrap items-center gap-2 rounded-2xl border border-line bg-white px-4 py-3">
              <span className="min-w-48 font-semibold">{person.name}</span>
              <span className="text-sm text-muted">{person.departmentName || "No department"}</span>
              <input className={inputClass} style={{ width: 180 }} value={person.employeeNumber ?? ""} aria-label={`Employee number for ${person.name}`} onChange={(event) => setPeople((current) => current.map((item) => item.userId === person.userId ? { ...item, employeeNumber: event.target.value } : item))} />
              <Button variant="ghost" onClick={() => onNumber(people.find((item) => item.userId === person.userId) ?? person)}>Save</Button>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}
