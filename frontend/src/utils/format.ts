export const TASK_STATUSES = ["Planned", "InProgress", "Completed", "Blocked", "Cancelled"] as const;
export const ENTRY_STATUSES = ["Completed", "InProgress", "Blocked"] as const;
export const PRIORITIES = ["Low", "Medium", "High", "Critical"] as const;
export const PROJECT_STATUSES = ["Active", "OnHold", "Completed", "Cancelled"] as const;

const LABELS: Record<string, string> = {
  InProgress: "In Progress",
  OnHold: "On Hold",
  TeamLead: "Team Lead",
  TeamMember: "Team Member",
  Admin: "Admin",
};

export function canManage(role?: string | null) {
  return role === "TeamLead" || role === "Admin";
}

export function label(value: string) {
  return LABELS[value] ?? value;
}

export function formatMinutes(minutes: number) {
  const total = Math.max(0, Math.round(minutes));
  const hours = Math.floor(total / 60);
  const rest = total % 60;
  if (hours === 0) return `${rest}m`;
  if (rest === 0) return `${hours}h`;
  return `${hours}h ${rest}m`;
}

export function formatHours(hours: number) {
  return `${Number(hours).toFixed(hours % 1 === 0 ? 0 : 1)}h`;
}

export function toMinutes(value: string) {
  const [hours, minutes] = value.split(":").map(Number);
  return hours * 60 + minutes;
}

export function rangesOverlap(start: string, end: string, otherStart: string, otherEnd: string) {
  return toMinutes(start) < toMinutes(otherEnd) && toMinutes(end) > toMinutes(otherStart);
}

export function todayInput() {
  const date = new Date();
  return toInputDate(date);
}

export function toInputDate(date: Date) {
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${date.getFullYear()}-${month}-${day}`;
}

export function currentMonthValue() {
  return todayInput().slice(0, 7);
}

export function parseMonth(value: string) {
  const [year, month] = value.split("-").map(Number);
  return { year, month };
}

export function monthLabel(year: number, month: number) {
  return new Date(year, month - 1, 1).toLocaleDateString(undefined, { month: "long", year: "numeric" });
}

export function longDate(value: string) {
  const [year, month, day] = value.split("-").map(Number);
  return new Date(year, month - 1, day).toLocaleDateString(undefined, {
    day: "2-digit",
    month: "long",
    year: "numeric",
  });
}

export function shortDate(value: string) {
  const [year, month, day] = value.split("-").map(Number);
  return new Date(year, month - 1, day).toLocaleDateString(undefined, { day: "numeric", month: "short" });
}

export function greeting(name: string) {
  const hour = new Date().getHours();
  const hello = hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";
  return `${hello}, ${name.split(" ")[0]}`;
}

export function firstName(name: string) {
  return name.split(" ")[0];
}
