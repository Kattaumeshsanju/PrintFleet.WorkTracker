import { lazy, Suspense } from "react";
import { Navigate, Outlet, Route, Routes } from "react-router-dom";
import { useAuth } from "./auth/AuthContext";
import { Loading } from "./components/ui";
import { canManage, canViewOrganization, hasOverview, hasReports } from "./utils/format";
import { AppLayout } from "./layouts/AppLayout";
import { LoginPage } from "./pages/LoginPage";
import { JoinPage } from "./pages/JoinPage";
import { ActivatePage } from "./pages/ActivatePage";

const DashboardPage = lazy(() => import("./pages/DashboardPage").then((module) => ({ default: module.DashboardPage })));
const DailyPage = lazy(() => import("./pages/DailyPage").then((module) => ({ default: module.DailyPage })));
const MonthlyPlanPage = lazy(() => import("./pages/MonthlyPlanPage").then((module) => ({ default: module.MonthlyPlanPage })));
const TeamPlansPage = lazy(() => import("./pages/TeamPlansPage").then((module) => ({ default: module.TeamPlansPage })));
const ProjectsPage = lazy(() => import("./pages/ProjectsPage").then((module) => ({ default: module.ProjectsPage })));
const InitiativesPage = lazy(() => import("./pages/InitiativesPage").then((module) => ({ default: module.InitiativesPage })));
const ReportPage = lazy(() => import("./pages/ReportPage").then((module) => ({ default: module.ReportPage })));
const SettingsPage = lazy(() => import("./pages/SettingsPage").then((module) => ({ default: module.SettingsPage })));
const ManageTeamPage = lazy(() => import("./pages/ManageTeamPage").then((module) => ({ default: module.ManageTeamPage })));
const TeamHistoryPage = lazy(() => import("./pages/ManageTeamPage").then((module) => ({ default: module.TeamHistoryPage })));
const OrganizationTeamHistoryPage = lazy(() => import("./pages/ManageTeamPage").then((module) => ({ default: function OrganizationTeamHistory() { return <module.TeamHistoryPage organization />; } })));
const TeamPage = lazy(() => import("./pages/TeamPage").then((module) => ({ default: module.TeamPage })));
const EmployeePage = lazy(() => import("./pages/EmployeePage").then((module) => ({ default: module.EmployeePage })));
const TeamReportPage = lazy(() => import("./pages/TeamReportPage").then((module) => ({ default: module.TeamReportPage })));
const OrganizationPage = lazy(() => import("./pages/OrganizationPage").then((module) => ({ default: module.OrganizationPage })));
const ExecutiveOverview = lazy(() => import("./pages/executive/ExecutiveOverview").then((module) => ({ default: module.ExecutiveOverview })));
const ExecutiveDepartments = lazy(() => import("./pages/executive/ExecutiveDepartments").then((module) => ({ default: module.ExecutiveDepartments })));
const ExecutiveDepartment = lazy(() => import("./pages/executive/ExecutiveDepartments").then((module) => ({ default: module.ExecutiveDepartment })));
const ExecutiveProjects = lazy(() => import("./pages/executive/ExecutiveProjects").then((module) => ({ default: module.ExecutiveProjects })));
const ExecutiveProject = lazy(() => import("./pages/executive/ExecutiveProjects").then((module) => ({ default: module.ExecutiveProject })));
const ExecutivePlans = lazy(() => import("./pages/executive/ExecutivePlans").then((module) => ({ default: module.ExecutivePlans })));
const ExecutiveRisks = lazy(() => import("./pages/executive/ExecutiveRisks").then((module) => ({ default: module.ExecutiveRisks })));
const ExecutiveReports = lazy(() => import("./pages/executive/ExecutiveReports").then((module) => ({ default: module.ExecutiveReports })));
const ExecutiveResearch = lazy(() => import("./pages/executive/ExecutiveResearch").then((module) => ({ default: module.ExecutiveResearch })));
const DepartmentsPage = lazy(() => import("./pages/DepartmentsPage").then((module) => ({ default: module.DepartmentsPage })));
const OrganizationAccessPage = lazy(() => import("./pages/OrganizationAccessPage").then((module) => ({ default: module.OrganizationAccessPage })));
const TeamTodayPage = lazy(() => import("./pages/TeamTodayPage").then((module) => ({ default: module.TeamTodayPage })));
const ReminderSettingsPage = lazy(() => import("./pages/ReminderSettingsPage").then((module) => ({ default: module.ReminderSettingsPage })));

function RequireAuth() {
  const { user, loading } = useAuth();
  if (loading) return <div className="grid min-h-screen place-items-center text-muted">Loading…</div>;
  if (!user) return <Navigate to="/login" replace />;
  return <Outlet />;
}

function RequireLead() {
  const { user } = useAuth();
  if (!canManage(user?.role)) return <Navigate to="/" replace />;
  return <Outlet />;
}

function RequireAdmin() {
  const { user } = useAuth();
  if (user?.role !== "Admin") return <Navigate to="/" replace />;
  return <Outlet />;
}

function RequireOverview() {
  const { user } = useAuth();
  if (!hasOverview(user)) return <Navigate to="/" replace />;
  return <Outlet />;
}

function RequireReports() {
  const { user } = useAuth();
  if (!hasReports(user)) return <Navigate to="/" replace />;
  return <Outlet />;
}

function RequireResearch() {
  const { user } = useAuth();
  if (!canViewOrganization(user?.role)) return <Navigate to="/" replace />;
  return <Outlet />;
}

function RequireTeamReports() {
  const { user } = useAuth();
  if (!canManage(user?.role) && !hasReports(user)) return <Navigate to="/" replace />;
  return <Outlet />;
}

function Home() {
  const { user } = useAuth();
  if (user?.role === "CEO") return <Navigate to="/executive" replace />;
  return (
    <Suspense fallback={<Loading label="Loading the page" />}>
      <DashboardPage />
    </Suspense>
  );
}

function RequireReviewer() {
  const { user } = useAuth();
  if (!canManage(user?.role) && !canViewOrganization(user?.role)) return <Navigate to="/" replace />;
  return <Outlet />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/join" element={<JoinPage />} />
      <Route path="/activate" element={<ActivatePage />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route index element={<Home />} />
          <Route path="monthly-plan" element={<MonthlyPlanPage />} />
          <Route path="plans" element={<TeamPlansPage />} />
          <Route path="daily" element={<DailyPage />} />
          <Route path="projects" element={<ProjectsPage />} />
          <Route path="initiatives" element={<InitiativesPage />} />
          <Route path="reports/me" element={<ReportPage />} />
          <Route path="settings" element={<SettingsPage />} />
          <Route path="my-team" element={<ManageTeamPage />} />
          <Route path="my-team/history" element={<TeamHistoryPage />} />
          <Route element={<RequireLead />}>
            <Route path="team" element={<TeamPage />} />
          </Route>
          <Route element={<RequireReviewer />}>
            <Route path="team/:userId" element={<EmployeePage />} />
          </Route>
          <Route element={<RequireTeamReports />}>
            <Route path="reports/team" element={<TeamReportPage />} />
          </Route>
          <Route element={<RequireOverview />}>
            <Route path="organization" element={<OrganizationPage />} />
            <Route path="executive" element={<ExecutiveOverview />} />
            <Route path="executive/departments" element={<ExecutiveDepartments />} />
            <Route path="executive/departments/:departmentId" element={<ExecutiveDepartment />} />
            <Route path="executive/projects" element={<ExecutiveProjects />} />
            <Route path="executive/projects/:projectId" element={<ExecutiveProject />} />
            <Route path="executive/plans" element={<ExecutivePlans />} />
            <Route path="executive/risks" element={<ExecutiveRisks />} />
          </Route>
          <Route element={<RequireReports />}>
            <Route path="executive/reports" element={<ExecutiveReports />} />
          </Route>
          <Route element={<RequireResearch />}>
            <Route path="executive/research" element={<ExecutiveResearch />} />
          </Route>
          <Route element={<RequireAdmin />}>
            <Route path="departments" element={<DepartmentsPage />} />
            <Route path="organization-access" element={<OrganizationAccessPage />} />
            <Route path="team-today" element={<TeamTodayPage />} />
            <Route path="team-notices" element={<OrganizationTeamHistoryPage />} />
            <Route path="settings/reminders" element={<ReminderSettingsPage />} />
          </Route>
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
