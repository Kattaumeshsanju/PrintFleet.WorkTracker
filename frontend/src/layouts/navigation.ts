import type { LucideIcon } from "lucide-react";
import { AlertTriangle, Building2, CalendarClock, ChartColumn, ClipboardList, FlaskConical, FolderKanban, Landmark, LayoutDashboard, Mail, Settings, Shield, Users } from "lucide-react";
import type { Role } from "../types";
import { hasOverview, hasReports } from "../utils/format";

export type Access = {
  role?: Role | null;
  organizationOverview?: boolean;
  organizationReports?: boolean;
};

export type NavItem = {
  to: string;
  label: string;
  icon: LucideIcon;
  end?: boolean;
  nest?: boolean;
  roles: Role[];
  allow?: (access: Access) => boolean;
};

export type NavSection = {
  id: string;
  label: string;
  items: NavItem[];
};

const navigation: NavSection[] = [
  {
    id: "workspace",
    label: "Workspace",
    items: [
      { to: "/", label: "Dashboard", icon: LayoutDashboard, end: true, roles: ["Admin", "TeamLead", "TeamMember"] },
      { to: "/daily", label: "My Daily Work", icon: CalendarClock, roles: ["Admin", "TeamLead", "TeamMember"] },
      { to: "/monthly-plan", label: "My Monthly Plan", icon: ClipboardList, roles: ["Admin", "TeamLead", "TeamMember"] },
      { to: "/reports/me", label: "My Reports", icon: ChartColumn, end: true, roles: ["Admin", "TeamLead", "TeamMember"] },
    ],
  },
  {
    id: "team",
    label: "Team Management",
    items: [
      { to: "/team", label: "Team", icon: Users, end: true, nest: true, roles: ["Admin", "TeamLead"] },
      { to: "/my-team", label: "Manage My Team", icon: Users, end: true, roles: ["TeamLead"] },
      { to: "/my-team/history", label: "Team Notices", icon: Mail, end: true, roles: ["TeamLead"] },
      { to: "/my-team", label: "My Team", icon: Users, end: true, roles: ["TeamMember"] },
      { to: "/team-notices", label: "Team Notices", icon: Mail, roles: ["Admin"] },
      { to: "/team-today", label: "Team Today", icon: CalendarClock, roles: ["Admin"] },
      { to: "/plans", label: "Department Monthly Plan", icon: ClipboardList, roles: ["Admin", "TeamLead", "TeamMember"] },
      { to: "/projects", label: "My Projects", icon: FolderKanban, roles: ["TeamMember"] },
      { to: "/projects", label: "Department Projects", icon: FolderKanban, roles: ["TeamLead"] },
      { to: "/projects", label: "Projects", icon: FolderKanban, roles: ["Admin"] },
      { to: "/initiatives", label: "Initiatives", icon: FlaskConical, roles: ["Admin", "TeamLead", "TeamMember"] },
      { to: "/reports/team", label: "Department Reports", icon: ChartColumn, end: true, roles: ["TeamLead"] },
    ],
  },
  {
    id: "organization",
    label: "Organization",
    items: [
      { to: "/organization-access", label: "Organization Access", icon: Shield, roles: ["Admin"] },
      { to: "/executive", label: "Executive Overview", icon: Landmark, end: true, roles: ["Admin", "CEO"], allow: hasOverview },
      { to: "/departments", label: "Departments", icon: Building2, roles: ["Admin"] },
      { to: "/executive/departments", label: "Departments", icon: Building2, end: true, nest: true, roles: ["CEO"], allow: (access) => hasOverview(access) && access.role !== "Admin" },
      { to: "/executive/projects", label: "Projects", icon: FolderKanban, end: true, nest: true, roles: ["CEO"], allow: (access) => hasOverview(access) && access.role !== "Admin" },
      { to: "/executive/plans", label: "Monthly Plans", icon: ClipboardList, roles: ["CEO"], allow: (access) => hasOverview(access) && access.role !== "Admin" },
      { to: "/reports/team", label: "Organization Reports", icon: ChartColumn, end: true, roles: ["Admin"] },
      { to: "/executive/reports", label: "Organization Reports", icon: ChartColumn, roles: ["CEO"], allow: (access) => hasReports(access) && access.role !== "Admin" },
      { to: "/executive/risks", label: "Risks and Delays", icon: AlertTriangle, roles: ["Admin", "CEO"], allow: hasOverview },
      { to: "/executive/research", label: "Research Portfolio", icon: FlaskConical, roles: ["Admin", "CEO"] },
    ],
  },
  {
    id: "system",
    label: "System",
    items: [
      { to: "/settings", label: "Settings", icon: Settings, roles: ["Admin", "CEO", "TeamLead", "TeamMember"] },
    ],
  },
];

export function isNavItemActive(pathname: string, item: Pick<NavItem, "to" | "nest">) {
  if (pathname === item.to) return true;
  return Boolean(item.nest && item.to !== "/" && pathname.startsWith(`${item.to}/`));
}

export function activeNavItem<T extends Pick<NavItem, "to" | "label" | "nest">>(pathname: string, items: T[]) {
  const matches = items.filter((item) => isNavItemActive(pathname, item));
  return matches.reduce<T | null>((best, item) => (!best || item.to.length > best.to.length ? item : best), null);
}

export function navigationFor(access?: Access | Role | null) {
  const value: Access = typeof access === "string" || access == null ? { role: access ?? null } : access;
  if (!value.role) return [];
  return navigation
    .map((section) => ({
      ...section,
      items: section.items.filter((item) => item.allow ? item.allow(value) : item.roles.includes(value.role as Role)),
    }))
    .filter((section) => section.items.length > 0);
}
