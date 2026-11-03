export type Role = "Admin" | "CEO" | "TeamLead" | "TeamMember";

export type Department = {
  id: string;
  name: string;
  isActive: boolean;
  teamLeadId: string | null;
  teamLeadName: string | null;
  memberCount: number;
  activeProjectCount: number;
};

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
  organizationOverview?: boolean;
  organizationReports?: boolean;
};

export type OrganizationPerson = {
  id: string;
  name: string;
  email: string;
  departmentName: string | null;
  role: Role;
  jobTitle: string | null;
  isActive: boolean;
  overview: boolean;
  reports: boolean;
  grantsEditable: boolean;
  statusEditable: boolean;
};

export type CeoInvitation = {
  id: string;
  name: string;
  email: string;
  jobTitle: string | null;
  activateOnAccept: boolean;
  expiresAt: string;
};

export type OrganizationAccess = {
  people: OrganizationPerson[];
  invitations: CeoInvitation[];
};

export type Project = {
  id: string;
  name: string;
  code: string;
  description: string | null;
  startDate: string | null;
  endDate: string | null;
  status: string;
  priority: string;
  ownerId: string | null;
  ownerName: string | null;
  departmentId: string | null;
  departmentName: string | null;
  workCategoryId: string | null;
  workCategoryName: string | null;
  createdAt: string;
};

export type MonthlyTask = {
  id: string;
  monthlyWorkSheetId: string;
  projectId: string | null;
  projectName: string | null;
  projectCode: string | null;
  workCategoryId: string | null;
  workCategoryName: string | null;
  initiativeId: string | null;
  initiativeNumber: string | null;
  initiativeTitle: string | null;
  title: string;
  description: string | null;
  priority: string;
  plannedStartDate: string;
  plannedEndDate: string;
  actualStartDate: string | null;
  actualEndDate: string | null;
  estimatedHours: number;
  actualHours: number;
  progressPercent: number;
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
  departmentName: string | null;
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
  id?: string | null;
  severity: "info" | "warning" | "critical" | string;
  title: string;
  message: string;
  href: string | null;
  unread?: boolean;
  senderName?: string | null;
  createdAt?: string | null;
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
  includesDailyMetrics: boolean;
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

export type DepartmentPlan = {
  id: string;
  departmentId: string;
  departmentName: string;
  year: number;
  month: number;
  status: string;
  summary: string | null;
  risks: string | null;
  comments: string | null;
};

export type TeamPlans = {
  year: number;
  month: number;
  departmentPlans: DepartmentPlan[];
  employees: EmployeeOverview[];
};

export type GoalAlert = {
  taskId: string;
  title: string;
  userId: string;
  employee: string;
  departmentId: string | null;
  department: string;
  projectId: string | null;
  project: string | null;
  due: string;
  status: string;
  remarks: string | null;
};

export type OrganizationDashboard = {
  year: number;
  month: number;
  departmentCount: number;
  activeProjectCount: number;
  plannedGoals: number;
  completedGoals: number;
  completionPercent: number;
  overdueGoals: number;
  blockedGoals: number;
  inProgressGoals: number;
  cancelledGoals: number;
  projectsAtRisk: number;
  hasGoals: boolean;
  previousCompletionPercent: number | null;
  completionChange: number | null;
  staleProgressDays: number;
  generatedAt: string;
  progressRule: string;
  departments: {
    departmentId: string;
    name: string;
    isActive: boolean;
    teamLeadName: string | null;
    activeProjects: number;
    plannedGoals: number;
    completedGoals: number;
    inProgressGoals: number;
    cancelledGoals: number;
    blockedGoals: number;
    overdueGoals: number;
    hasGoals: boolean;
    hasMonthlyPlan: boolean;
    completionPercent: number;
  }[];
  projects: {
    projectId: string;
    name: string;
    departmentId: string | null;
    departmentName: string | null;
    ownerName: string | null;
    status: string;
    priority: string;
    startDate: string | null;
    endDate: string | null;
    progressPercent: number;
    hasGoals: boolean;
    atRisk: boolean;
    riskSummary: string | null;
    plannedGoals: number;
    completedGoals: number;
    inProgressGoals: number;
    blockedGoals: number;
    overdueGoals: number;
  }[];
  overdue: GoalAlert[];
  upcoming: GoalAlert[];
  blockers: GoalAlert[];
  workload: { userId: string; name: string; department: string | null; goals: number; completed: number; blocked: number }[];
  trend: { year: number; month: number; plannedGoals: number; completedGoals: number; completionPercent: number }[];
  plans: DepartmentPlan[];
  risks: ExecutiveRisk[];
};

export type ExecutiveRisk = {
  kind: string;
  severity: string;
  title: string;
  departmentId: string | null;
  department: string | null;
  projectId: string | null;
  project: string | null;
  userId: string | null;
  due: string | null;
  status: string | null;
  summary: string | null;
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

export type WorkCategory = {
  id: string;
  departmentId: string;
  departmentName: string;
  name: string;
  description: string | null;
  isActive: boolean;
  displayOrder: number;
  createdByName: string | null;
  createdAt: string;
};

export type Initiative = {
  id: string;
  number: string;
  title: string;
  description: string | null;
  departmentId: string;
  departmentName: string;
  workCategoryId: string | null;
  workCategoryName: string | null;
  projectId: string | null;
  projectName: string | null;
  status: string;
  priority: string;
  ownerId: string | null;
  ownerName: string | null;
  collaborators: { id: string; name: string }[];
  targetStart: string | null;
  targetEnd: string | null;
  remarks: string | null;
  researchObjective: string | null;
  hypothesis: string | null;
  researchStage: string | null;
  successCriteria: string | null;
  experimentPlan: string | null;
  conclusion: string | null;
  canEdit: boolean;
  canRecord: boolean;
  updatedAt: string;
  milestones: Milestone[];
  records: ResearchRecord[];
};

export type Milestone = {
  id: string;
  title: string;
  description: string | null;
  ownerId: string | null;
  ownerName: string | null;
  targetDate: string | null;
  status: string;
  completedOn: string | null;
};

export type ResearchRecord = {
  id: string;
  title: string;
  recordedOn: string;
  objective: string | null;
  method: string | null;
  findings: string | null;
  outcome: string | null;
  evidenceLinks: string | null;
  nextSteps: string | null;
  createdByName: string | null;
  createdAt: string;
};

export type ResearchPortfolio = {
  activeInitiatives: number;
  onHold: number;
  completedInPeriod: number;
  milestonesDue: number;
  milestonesOverdue: number;
  progressRule: string;
  byStatus: { name: string; count: number }[];
  byStage: { name: string; count: number }[];
  byPriority: { name: string; count: number }[];
  categories: { departmentId: string; departmentName: string; categoryId: string; categoryName: string; counted: number; completed: number; completionPercent: number | null }[];
  deadlines: Initiative[];
  outcomes: ResearchRecord[];
};

export type LoginResponse = { token: string; expiresAt: string; user: User };

export type TeamPerson = {
  id: string;
  name: string;
  email: string;
  jobTitle: string | null;
  role: Role;
  isActive: boolean;
  departmentName: string | null;
  departmentId: string | null;
};

export type TeamInvite = {
  id: string;
  name: string;
  email: string;
  jobTitle: string | null;
  status: string;
  expiresAt: string;
};

export type MyTeam = {
  departmentId: string;
  departmentName: string;
  teamLeadName: string | null;
  activeMembers: number;
  pendingInvitations: number;
  activeProjects: number;
  canManage: boolean;
  members: TeamPerson[];
  invitations: TeamInvite[];
  projects: { id: string; name: string }[];
};

export type TeamNotice = {
  id: string;
  title: string;
  body: string;
  category: string;
  senderName: string;
  createdAt: string;
  recipientCount: number;
  channel: string;
  status: string;
  failedRecipients: string[];
  canRetry: boolean;
};
