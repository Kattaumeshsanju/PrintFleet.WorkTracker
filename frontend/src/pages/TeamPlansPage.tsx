import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { Badge, Loading, PageHeader } from "../components/ui";
import { api, errorText } from "../services/api";
import type { Project, TeamPlans, User } from "../types";
import { currentMonthValue, formatHours, monthLabel, parseMonth } from "../utils/format";

export function TeamPlansPage() {
  const { user } = useAuth();
  const lead = user?.role === "TeamLead";
  const [month, setMonth] = useState(currentMonthValue());
  const [userId, setUserId] = useState("");
  const [projectId, setProjectId] = useState("");
  const [status, setStatus] = useState("");
  const [priority, setPriority] = useState("");
  const [search, setSearch] = useState("");
  const [plans, setPlans] = useState<TeamPlans | null>(null);
  const [people, setPeople] = useState<User[]>([]);
  const [projects, setProjects] = useState<Project[]>([]);
  const [error, setError] = useState("");

  useEffect(() => {
    if (lead) api<User[]>("/api/users").then(setPeople).catch(() => undefined);
    api<Project[]>("/api/projects").then(setProjects).catch(() => undefined);
  }, [lead]);

  useEffect(() => {
    const { year, month: monthNumber } = parseMonth(month);
    const query = new URLSearchParams({ year: String(year), month: String(monthNumber) });
    if (userId) query.set("userId", userId);
    if (projectId) query.set("projectId", projectId);
    if (status) query.set("status", status);
    if (priority) query.set("priority", priority);
    if (search.trim()) query.set("search", search.trim());
    api<TeamPlans>(`/api/monthly-work/team?${query}`).then(setPlans).catch((err) => setError(errorText(err)));
  }, [month, userId, projectId, status, priority, search]);

  if (error) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;
  if (!plans) return <Loading />;
  const directory = people.length > 0 ? people : plans.employees.map((employee) => employee.user);

  return (
    <div>
      <PageHeader title="Monthly Plans" subtitle={monthLabel(plans.year, plans.month)} />
      <div className="mb-4 grid gap-2 md:grid-cols-3 xl:grid-cols-6">
        <input className="rounded-xl border border-line bg-white px-3 py-2" type="month" value={month} onChange={(event) => setMonth(event.target.value)} />
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={userId} onChange={(event) => setUserId(event.target.value)}>
          <option value="">All employees</option>
          {directory.map((person) => <option key={person.id} value={person.id}>{person.name}</option>)}
        </select>
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={projectId} onChange={(event) => setProjectId(event.target.value)}>
          <option value="">All projects</option>
          {projects.map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}
        </select>
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">All statuses</option>
          {["Planned", "InProgress", "Completed", "Blocked", "Cancelled"].map((item) => <option key={item} value={item}>{item === "InProgress" ? "In Progress" : item}</option>)}
        </select>
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={priority} onChange={(event) => setPriority(event.target.value)}>
          <option value="">All priorities</option>
          {["Low", "Medium", "High", "Critical"].map((item) => <option key={item}>{item}</option>)}
        </select>
        <input className="rounded-xl border border-line bg-white px-3 py-2" placeholder="Search tasks or people" value={search} onChange={(event) => setSearch(event.target.value)} />
      </div>
      <div className="space-y-4">
        {plans.employees.map((employee) => (
          <section key={employee.user.id} className="rounded-2xl border border-line bg-card p-4">
            <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
              <div>
                {lead ? <Link className="font-display text-xl font-semibold" to={`/team/${employee.user.id}`}>{employee.user.name}</Link> : <div className="font-display text-xl font-semibold">{employee.user.name}</div>}
                <div className="text-xs text-muted">{employee.plannedTasks} tasks · {employee.completionPercent}% · {formatHours(employee.actualHours)} actual</div>
              </div>
              <div className="flex gap-2">
                <Badge value={employee.sheetStatus} />
                <Link className="rounded-xl border border-line px-3 py-1.5 text-sm font-semibold" to={`/monthly-plan?userId=${employee.user.id}`}>Open plan</Link>
              </div>
            </div>
            {employee.tasks.length === 0 ? <div className="text-sm text-muted">No matching tasks.</div> : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[720px] text-left text-sm">
                  <thead className="text-xs text-muted uppercase"><tr><th className="py-1">Task</th><th>Project</th><th>Priority</th><th>Status</th><th>Hours</th></tr></thead>
                  <tbody>
                    {employee.tasks.map((task) => (
                      <tr key={task.id} className="border-t border-line">
                        <td className="py-2 font-medium">{task.title}</td>
                        <td>{task.projectName ?? "—"}</td>
                        <td><Badge value={task.priority} /></td>
                        <td><Badge value={task.status} /></td>
                        <td className="font-mono">{task.actualHours}/{task.estimatedHours}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        ))}
      </div>
    </div>
  );
}
