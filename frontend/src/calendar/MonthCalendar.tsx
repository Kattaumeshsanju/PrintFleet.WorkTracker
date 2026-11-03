import type { CalendarDay } from "../types";
import { formatMinutes } from "../utils/format";

const weekdays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

export function MonthCalendar({ days, selected, userName, onPick }: { days: CalendarDay[]; selected?: string; userName?: string; onPick: (date: string) => void }) {
  if (days.length === 0) return null;
  const first = new Date(`${days[0].date}T00:00:00`);
  const pad = (first.getDay() + 6) % 7;
  const cells: Array<CalendarDay | null> = [...Array(pad).fill(null), ...days];
  const submitted = days.filter((day) => day.submitted);

  return (
    <div>
      <p className="mb-3 text-sm text-muted">
        {submitted.length === 0
          ? (userName ? `${userName} has no submitted dates this month.` : "No dates are submitted this month.")
          : `${userName ? `${userName} submitted` : "Submitted"} ${submitted.length} ${submitted.length === 1 ? "date" : "dates"}: ${submitted.map((day) => Number(day.date.slice(8, 10))).join(", ")}.`}
      </p>
      <div className="mb-3 flex flex-wrap gap-3 text-xs text-muted">
        <span className="inline-flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-emerald-600" /> Submitted</span>
        <span className="inline-flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-amber-500" /> Missing</span>
        <span className="inline-flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-orange-400" /> Under 6h</span>
        <span className="inline-flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-brass" /> Holiday</span>
        <span className="inline-flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-sky-600" /> Leave</span>
      </div>
      <div className="grid grid-cols-7 gap-2 text-center text-xs font-semibold tracking-wide text-muted uppercase">
        {weekdays.map((day) => <div key={day}>{day}</div>)}
      </div>
      <div className="mt-2 grid grid-cols-7 gap-2">
        {cells.map((day, index) => day ? (
          <button
            key={day.date}
            type="button"
            onClick={() => onPick(day.date)}
            className={`min-h-20 rounded-xl border p-2 text-left ${day.submitted ? "border-emerald-700 bg-emerald-600 text-white" : day.dayType === "Holiday" ? "border-amber-200 bg-amber-50" : day.dayType === "Leave" ? "border-sky-200 bg-sky-50" : "border-line bg-card"} ${day.date === selected ? "ring-2 ring-brass ring-offset-2" : ""} ${day.isWeekend && !day.dayType && !day.submitted ? "opacity-70" : ""}`}
          >
            <div className="flex items-center justify-between text-xs">
              <span className="font-semibold">{Number(day.date.slice(8, 10))}</span>
              <span className={`h-2.5 w-2.5 rounded-full ${day.submitted ? "bg-white" : day.dayType === "Holiday" ? "bg-brass" : day.dayType === "Leave" ? "bg-sky-600" : day.isWeekend ? "bg-stone-300" : day.missing ? "bg-amber-500" : day.lowHours ? "bg-orange-400" : "bg-pine"}`} />
            </div>
            <div className={`mt-3 font-mono text-sm ${day.submitted ? "text-white" : ""}`}>{day.dayType ? day.dayType : day.minutes > 0 ? formatMinutes(day.minutes) : day.isWeekend ? "Weekend" : "—"}</div>
            <div className={`text-[11px] font-semibold ${day.submitted ? "text-white" : "font-normal text-muted"}`}>{day.submitted ? "Submitted" : day.dayType ? day.dayType : day.isWeekend ? "Weekend" : day.missing ? "Missing" : "Open"}</div>
          </button>
        ) : <div key={`pad-${index}`} />)}
      </div>
    </div>
  );
}
