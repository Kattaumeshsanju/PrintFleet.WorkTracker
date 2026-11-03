export const TASK_STATUSES = ["Planned", "InProgress", "Completed", "Blocked", "Cancelled"] as const;
export const ENTRY_STATUSES = ["Completed", "InProgress", "Blocked"] as const;
export const PRIORITIES = ["Low", "Medium", "High", "Critical"] as const;
export const PROJECT_STATUSES = ["Active", "OnHold", "Completed", "Cancelled"] as const;

const LABELS: Record<string, string> = {
  InProgress: "In Progress",
  OnHold: "On Hold",
  Ongoing: "Ongoing",
  ProofOfConcept: "Proof of Concept",
  TeamLead: "Team Lead",
  TeamMember: "Employee",
  Admin: "Admin",
  CEO: "CEO",
  InReview: "In Review",
  SentToProvider: "Sent to mail provider",
  InApp: "In app",
  Partial: "Partial",
  Bounced: "Bounced",
  General: "General Announcement",
  Project: "Project Update",
  Plan: "Monthly Plan",
  Deadline: "Deadline Reminder",
  Meeting: "Meeting",
  Blocker: "Blocker",
  Email: "Email",
  Both: "Email and in-app",
  CompOff: "Compensatory Off",
  OD: "Outstation Duty",
  WeeklyOff: "Weekly Off",
  NotMarked: "Not Marked",
  SecondAndFourth: "2nd and 4th Saturday off",
  FirstAndThird: "1st and 3rd Saturday off",
  AllOff: "Every Saturday off",
  None: "No extra Saturday off",
};

export function goalCompletion(percent: number, counted: number) {
  return counted === 0 ? "No goals" : `${percent}%`;
}

export function canManage(role?: string | null) {
  return role === "TeamLead" || role === "Admin";
}

export function canViewOrganization(role?: string | null) {
  return role === "Admin" || role === "CEO";
}

export function hasOverview(user?: { role?: string | null; organizationOverview?: boolean } | null) {
  return Boolean(user?.organizationOverview) || canViewOrganization(user?.role);
}

export function hasReports(user?: { role?: string | null; organizationReports?: boolean } | null) {
  return Boolean(user?.organizationReports) || canViewOrganization(user?.role);
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
