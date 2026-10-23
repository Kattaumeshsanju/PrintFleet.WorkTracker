export type Role = "Admin" | "TeamLead" | "TeamMember";

export type Department = { id: string; name: string };

export type User = {
  id: string;
  departmentId: string | null;
  departmentName: string | null;
  name: string;
  email: string;
  role: Role;
  jobTitle: string | null;
  isActive: boolean;
  createdAt: string;
};

export type Project = {
  id: string;
  name: string;
  code: string;
  description: string | null;
  startDate: string | null;
  endDate: string | null;
  status: string;
  departmentId: string | null;
  departmentName: string | null;
  createdAt: string;
};

export type MonthlyTask = {
  id: string;
  monthlyWorkSheetId: string;
  projectId: string | null;
  projectName: string | null;
  projectCode: string | null;
  title: string;
  description: string | null;
  priority: string;
  plannedStartDate: string;
  plannedEndDate: string;
  estimatedHours: number;
  actualHours: number;
  status: string;
  remarks: string | null;
  overdue: boolean;
  createdAt: string;
  updatedAt: string;
};

export type MonthlySheet = {
  id: string;
  userId: string;
  userName: string;
  jobTitle: string | null;
  year: number;
  month: number;
  status: string;
  lockedAt: string | null;
  plannedTasks: number;
  completed: number;
  inProgress: number;
  blocked: number;
  cancelled: number;
  planned: number;
  plannedHours: number;
  actualHours: number;
  completionPercent: number;
  tasks: MonthlyTask[];
};

export type DailyEntry = {
  id: string;
  userId: string;
  userName: string;
  monthlyTaskId: string;
  monthlyTaskTitle: string;
  projectId: string | null;
  projectName: string | null;
  workDate: string;
  startTime: string;
  endTime: string;
  durationMinutes: number;
  description: string;
  status: string;
  remarks: string | null;
};

export type DayView = {
  date: string;
  userId: string;
  userName: string;
  jobTitle: string | null;
  totalMinutes: number;
  submissionStatus: string;
  submittedAt: string | null;
  monthLocked: boolean;
  canEdit: boolean;
  dayType: string | null;
  entries: DailyEntry[];
};

export type MonthProgress = {
  plannedTasks: number;
  completed: number;
  inProgress: number;
  blocked: number;
  cancelled: number;
  planned: number;
  plannedHours: number;
  actualHours: number;
  completionPercent: number;
};

export type Notice = {
  severity: "info" | "warning" | "critical" | string;
  title: string;
  message: string;
  href: string | null;
};

export type MyDashboard = {
  date: string;
  todayMinutes: number;
  tasksCompleted: number;
  tasksInProgress: number;
  dailyUpdateStatus: string;
  monthLocked: boolean;
  today: DayView;
  month: MonthProgress;
  upcomingTasks: MonthlyTask[];
  notifications: Notice[];
};

export type TeamMemberDay = {
  userId: string;
  name: string;
  jobTitle: string | null;
  role: Role;
  updateStatus: string;
  totalMinutes: number;
  currentActivity: string | null;
  currentTask: string | null;
  currentStart: string | null;
  currentEnd: string | null;
  currentStatus: string | null;
  entries: DailyEntry[];
};

export type TeamDashboard = {
  date: string;
  memberCount: number;
  updatedToday: number;
  totalMinutesToday: number;
  openTasks: number;
  completedThisMonth: number;
  blockedTasks: number;
  members: TeamMemberDay[];
  notifications: Notice[];
};

export type CalendarDay = {
  date: string;
  minutes: number;
  entryCount: number;
  submitted: boolean;
  isWeekend: boolean;
  missing: boolean;
  lowHours: boolean;
  dayType: string | null;
};

export type CalendarMonth = {
  year: number;
  month: number;
  userId: string;
  userName: string;
  days: CalendarDay[];
};

export type StatusCount = { status: string; count: number };
export type DailyHoursPoint = { date: string; minutes: number; submitted: boolean; isWeekend: boolean };

export type MonthlyReport = {
  employee: User;
  year: number;
  month: number;
  sheetStatus: string;
  plannedTasks: number;
  completed: number;
  inProgress: number;
  blocked: number;
  cancelled: number;
  planned: number;
  plannedHours: number;
  actualHours: number;
  averageDailyHours: number;
  completionPercent: number;
  daysLogged: number;
  statusDistribution: StatusCount[];
  dailyHours: DailyHoursPoint[];
  tasks: MonthlyTask[];
};

export type TeamReportRow = {
  userId: string;
  name: string;
  jobTitle: string | null;
  role: Role;
  planned: number;
  completed: number;
  inProgress: number;
  blocked: number;
  cancelled: number;
  plannedHours: number;
  actualHours: number;
  completionPercent: number;
  availableDays: number;
  daysLogged: number;
  daysSubmitted: number;
  missingDays: number;
  lowHourDays: number;
  leaveDays: number;
  holidayDays: number;
  hoursAttainment: number;
  utilizationPercent: number;
  updateCompliance: number;
};

export type TeamReport = {
  year: number;
  month: number;
  includesSelf: boolean;
  plannedTasks: number;
  completedTasks: number;
  plannedHours: number;
  actualHours: number;
  completionPercent: number;
  availableDays: number;
  daysLogged: number;
  missingDays: number;
  lowHourDays: number;
  leaveDays: number;
  holidayDays: number;
  blockedTasks: number;
  hoursAttainment: number;
  utilizationPercent: number;
  updateCompliance: number;
  blockedPercent: number;
  expectedHoursPerDay: number;
  rows: TeamReportRow[];
  statusDistribution: StatusCount[];
};

export type EmployeeOverview = {
  user: User;
  year: number;
  month: number;
  sheetStatus: string;
  sheetId: string | null;
  plannedTasks: number;
  completed: number;
  inProgress: number;
  blocked: number;
  cancelled: number;
  planned: number;
  plannedHours: number;
  actualHours: number;
  completionPercent: number;
  tasks: MonthlyTask[];
  recentEntries: DailyEntry[];
  hoursByDay: CalendarDay[];
  completedWork: MonthlyTask[];
  blockedWork: MonthlyTask[];
  remarks: string[];
};

export type TeamPlans = {
  year: number;
  month: number;
  employees: EmployeeOverview[];
};

export type AuditLog = {
  id: number;
  userId: string | null;
  userName: string | null;
  action: string;
  entityType: string;
  entityId: string | null;
  details: string | null;
  createdAt: string;
};

export type Paged<T> = { items: T[]; total: number; page: number; pageSize: number };

export type LoginResponse = { token: string; expiresAt: string; user: User };
