import { useMemo } from "react";
import type { DailyEntry } from "../types";
import { formatMinutes } from "../utils/format";

const colors: Record<string, string> = {
  Completed: "bg-pine text-white",
  InProgress: "bg-sky-700 text-white",
  Blocked: "bg-amber-600 text-white",
};

function minutes(value: string) {
  const [hours, mins] = value.split(":").map(Number);
  return hours * 60 + mins;
}

export function DayTimeline({ entries, dayType, onSelect }: { entries: DailyEntry[]; dayType?: string | null; onSelect?: (entry: DailyEntry) => void }) {
  const bounds = useMemo(() => {
    if (entries.length === 0) return { start: 8 * 60, end: 18 * 60 };
    const starts = entries.map((entry) => minutes(entry.startTime));
    const ends = entries.map((entry) => minutes(entry.endTime));
    return {
      start: Math.min(8 * 60, ...starts),
      end: Math.max(18 * 60, ...ends),
    };
  }, [entries]);
  const span = Math.max(60, bounds.end - bounds.start);
  const ticks: number[] = [];
  for (let tick = Math.floor(bounds.start / 60) * 60; tick <= bounds.end; tick += 60) ticks.push(tick);

  if (entries.length === 0) {
    return <div className={`rounded-xl border border-dashed px-4 py-8 text-center text-sm ${dayType === "Holiday" ? "border-amber-300 bg-amber-50 text-amber-950" : dayType === "Leave" ? "border-sky-300 bg-sky-50 text-sky-950" : "border-line text-muted"}`}>{dayType ? `Marked as ${dayType.toLowerCase()}.` : "No activities on this day yet."}</div>;
  }

  return (
    <div>
      <div className="relative mb-1 h-5">
        {ticks.map((tick) => (
          <span key={tick} className="absolute -translate-x-1/2 font-mono text-[11px] text-muted" style={{ left: `${((tick - bounds.start) / span) * 100}%` }}>
            {String(Math.floor(tick / 60)).padStart(2, "0")}:00
          </span>
        ))}
      </div>
      <div className="relative h-16 rounded-xl bg-[#efeadd]">
        {entries.map((entry) => {
          const left = ((minutes(entry.startTime) - bounds.start) / span) * 100;
          const width = Math.max(2.5, ((minutes(entry.endTime) - minutes(entry.startTime)) / span) * 100);
          return (
            <button
              key={entry.id}
              type="button"
              title={`${entry.startTime}–${entry.endTime} ${entry.description}`}
              onClick={() => onSelect?.(entry)}
              className={`absolute top-2 bottom-2 overflow-hidden rounded-lg px-2 text-left text-xs font-semibold shadow-sm ${colors[entry.status] ?? "bg-stone-600 text-white"}`}
              style={{ left: `${left}%`, width: `${width}%` }}
            >
              <span className="block truncate">{entry.monthlyTaskTitle}</span>
              <span className="block truncate font-normal opacity-90">{formatMinutes(entry.durationMinutes)}</span>
            </button>
          );
        })}
      </div>
    </div>
  );
}

export function EntryList({ entries, onSelect }: { entries: DailyEntry[]; onSelect?: (entry: DailyEntry) => void }) {
  return (
    <div className="divide-y divide-line">
      {entries.map((entry) => (
        <button key={entry.id} type="button" onClick={() => onSelect?.(entry)} className="flex w-full items-start gap-4 py-3 text-left hover:bg-[#faf7f1]">
          <div className="w-28 shrink-0 font-mono text-sm text-ink">{entry.startTime}–{entry.endTime}</div>
          <div className="min-w-0 flex-1">
            <div className="font-semibold">{entry.monthlyTaskTitle}</div>
            <div className="truncate text-sm text-muted">{entry.description}</div>
          </div>
          <div className="shrink-0 font-mono text-sm text-muted">{formatMinutes(entry.durationMinutes)}</div>
        </button>
      ))}
    </div>
  );
}
