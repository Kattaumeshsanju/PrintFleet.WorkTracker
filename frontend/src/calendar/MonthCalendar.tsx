import type { CalendarDay } from "../types";
import { formatMinutes } from "../utils/format";

const weekdays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

export function MonthCalendar({ days, selected, onPick }: { days: CalendarDay[]; selected?: string; onPick: (date: string) => void }) {
  if (days.length === 0) return null;
  const first = new Date(`${days[0].date}T00:00:00`);
  const pad = (first.getDay() + 6) % 7;
  const cells: Array<CalendarDay | null> = [...Array(pad).fill(null), ...days];

  return (
    <div>
      <div className="grid grid-cols-7 gap-2 text-center text-xs font-semibold tracking-wide text-muted uppercase">
        {weekdays.map((day) => <div key={day}>{day}</div>)}
      </div>
      <div className="mt-2 grid grid-cols-7 gap-2">
        {cells.map((day, index) => day ? (
          <button
            key={day.date}
            type="button"
            onClick={() => onPick(day.date)}
            className={`min-h-20 rounded-xl border p-2 text-left ${day.date === selected ? "border-brass bg-[#fff6e8]" : day.dayType === "Holiday" ? "border-amber-200 bg-amber-50" : day.dayType === "Leave" ? "border-sky-200 bg-sky-50" : "border-line bg-card"} ${day.isWeekend && !day.dayType ? "opacity-70" : ""}`}
          >
            <div className="flex items-center justify-between text-xs">
              <span className="font-semibold">{Number(day.date.slice(8))}</span>
              <span className={`h-2 w-2 rounded-full ${day.dayType === "Holiday" ? "bg-brass" : day.dayType === "Leave" ? "bg-sky-600" : day.isWeekend ? "bg-stone-300" : day.missing ? "bg-amber-500" : day.lowHours ? "bg-orange-400" : "bg-pine"}`} />
            </div>
            <div className="mt-3 font-mono text-sm">{day.dayType ? day.dayType : day.minutes > 0 ? formatMinutes(day.minutes) : day.isWeekend ? "Weekend" : "—"}</div>
            <div className="text-[11px] text-muted">{day.dayType ? "Marked" : day.isWeekend ? "Weekend" : day.missing ? "Missing" : day.submitted ? "Updated" : "Open"}</div>
          </button>
        ) : <div key={`pad-${index}`} />)}
      </div>
    </div>
  );
}
