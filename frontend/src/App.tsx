import { Navigate, Outlet, Route, Routes } from "react-router-dom";
import { useAuth } from "./auth/AuthContext";
import { AppLayout } from "./layouts/AppLayout";
import { DailyPage } from "./pages/DailyPage";
import { DashboardPage } from "./pages/DashboardPage";
import { EmployeePage } from "./pages/EmployeePage";
import { LoginPage } from "./pages/LoginPage";
import { MonthlyPlanPage } from "./pages/MonthlyPlanPage";
import { ProjectsPage } from "./pages/ProjectsPage";
import { ReportPage } from "./pages/ReportPage";
import { SettingsPage } from "./pages/SettingsPage";
import { TeamPage } from "./pages/TeamPage";
import { TeamPlansPage } from "./pages/TeamPlansPage";
import { TeamReportPage } from "./pages/TeamReportPage";
import { TeamTodayPage } from "./pages/TeamTodayPage";

function RequireAuth() {
  const { user, loading } = useAuth();
  if (loading) return <div className="grid min-h-screen place-items-center text-muted">Loading…</div>;
  if (!user) return <Navigate to="/login" replace />;
  return <Outlet />;
}

function RequireLead() {
  const { user } = useAuth();
  if (user?.role !== "TeamLead") return <Navigate to="/" replace />;
  return <Outlet />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route index element={<DashboardPage />} />
          <Route path="monthly-plan" element={<MonthlyPlanPage />} />
          <Route path="plans" element={<TeamPlansPage />} />
          <Route path="daily" element={<DailyPage />} />
          <Route path="reports/me" element={<ReportPage />} />
          <Route path="settings" element={<SettingsPage />} />
          <Route element={<RequireLead />}>
            <Route path="team" element={<TeamPage />} />
            <Route path="team/:userId" element={<EmployeePage />} />
            <Route path="team-today" element={<TeamTodayPage />} />
            <Route path="projects" element={<ProjectsPage />} />
            <Route path="reports/team" element={<TeamReportPage />} />
          </Route>
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
