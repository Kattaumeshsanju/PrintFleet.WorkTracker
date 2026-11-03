import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { Badge, Button, Field, Loading, PageHeader, inputClass, useToast } from "../components/ui";
import { api, errorText } from "../services/api";
import type { Department, DepartmentPlan, Project, TeamPlans, User } from "../types";
import { canManage, canViewOrganization, currentMonthValue, goalCompletion, label, monthLabel, parseMonth } from "../utils/format";

const planStatuses = ["Open", "InReview", "Closed"];

export function TeamPlansPage() {
  const { user } = useAuth();
  const toast = useToast();
  const lead = canManage(user?.role);
  const viewer = canViewOrganization(user?.role);
  const [params, setParams] = useSearchParams();
  const month = params.get("month") || currentMonthValue();
  const departmentId = params.get("departmentId") ?? "";
  const userId = params.get("userId") ?? "";
  const projectId = params.get("projectId") ?? "";
  const status = params.get("status") ?? "";
  const priority = params.get("priority") ?? "";
  const search = params.get("search") ?? "";
  const [departments, setDepartments] = useState<Department[]>([]);
  const [plans, setPlans] = useState<TeamPlans | null>(null);
  const [people, setPeople] = useState<User[]>([]);
  const [projects, setProjects] = useState<Project[]>([]);
  const [error, setError] = useState("");
  const [refresh, setRefresh] = useState(0);

  function setFilter(key: string, value: string) {
    const next = new URLSearchParams(params);
    if (value) next.set(key, value);
    else next.delete(key);
    setParams(next, { replace: true });
  }

  useEffect(() => {
    if (lead || viewer) api<User[]>("/api/users").then(setPeople).catch(() => undefined);
    if (viewer) api<Department[]>("/api/departments").then(setDepartments).catch(() => undefined);
  }, [lead, viewer]);

  useEffect(() => {
    const query = departmentId ? `?departmentId=${departmentId}` : "";
    api<Project[]>(`/api/projects${query}`).then(setProjects).catch(() => undefined);
  }, [departmentId]);

  useEffect(() => {
    const { year, month: monthNumber } = parseMonth(month);
    const query = new URLSearchParams({ year: String(year), month: String(monthNumber) });
    if (departmentId) query.set("departmentId", departmentId);
    if (userId) query.set("userId", userId);
    if (projectId) query.set("projectId", projectId);
    if (status) query.set("status", status);
    if (priority) query.set("priority", priority);
    if (search.trim()) query.set("search", search.trim());
    api<TeamPlans>(`/api/monthly-work/team?${query}`).then(setPlans).catch((err) => setError(errorText(err)));
  }, [month, departmentId, userId, projectId, status, priority, search, refresh]);

  async function openPlan() {
    const target = departmentId || user?.departmentId || "";
    if (!target) {
      toast("Choose a department before opening its monthly plan.", "error");
      return;
    }
    const { year, month: monthNumber } = parseMonth(month);
    try {
      await api("/api/department-plans", { method: "POST", body: JSON.stringify({ year, month: monthNumber, departmentId: target, status: "Open" }) });
      toast("Department plan opened.");
      setRefresh((value) => value + 1);
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  if (error) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;
  if (!plans) return <Loading />;
  const directory = people.length > 0 ? people : plans.employees.map((employee) => employee.user);
  const visiblePlans = departmentId ? plans.departmentPlans.filter((plan) => plan.departmentId === departmentId) : plans.departmentPlans;
  const selectedDepartment = departmentId || user?.departmentId || "";
  const canOpen = lead && selectedDepartment && !plans.departmentPlans.some((plan) => plan.departmentId === selectedDepartment);

  return (
    <div>
      <PageHeader title="Department Monthly Plan" subtitle={`${monthLabel(plans.year, plans.month)} · one plan for the department. Each person's goals stay on their own sheet, separate from daily work.`} actions={canOpen ? <Button onClick={openPlan}>Open this month</Button> : undefined} />
      <div className="mb-4 grid gap-2 md:grid-cols-3 xl:grid-cols-6">
        <input className="rounded-xl border border-line bg-white px-3 py-2" type="month" value={month} onChange={(event) => setFilter("month", event.target.value)} />
        {viewer ? <select className="rounded-xl border border-line bg-white px-3 py-2" value={departmentId} onChange={(event) => setFilter("departmentId", event.target.value)}><option value="">All departments</option>{departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}</select> : <div className="rounded-xl border border-line bg-white px-3 py-2 text-sm">{user?.departmentName ?? "Your department"}</div>}
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={userId} onChange={(event) => setFilter("userId", event.target.value)}>
          <option value="">All employees</option>
          {directory.map((person) => <option key={person.id} value={person.id}>{person.name}</option>)}
        </select>
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={projectId} onChange={(event) => setFilter("projectId", event.target.value)}>
          <option value="">All projects</option>
          {projects.map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}
        </select>
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={status} onChange={(event) => setFilter("status", event.target.value)}>
          <option value="">All statuses</option>
          {["Planned", "InProgress", "Completed", "Blocked", "Cancelled"].map((item) => <option key={item} value={item}>{label(item)}</option>)}
        </select>
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={priority} onChange={(event) => setFilter("priority", event.target.value)}>
          <option value="">All priorities</option>
          {["Low", "Medium", "High", "Critical"].map((item) => <option key={item}>{item}</option>)}
        </select>
        <input className="rounded-xl border border-line bg-white px-3 py-2" placeholder="Search goals or people" value={search} onChange={(event) => setFilter("search", event.target.value)} />
      </div>
      {visiblePlans.length === 0 ? <div className="mb-4 rounded-2xl border border-line bg-card px-4 py-3 text-sm text-muted">No department plan is open for this month yet.</div> : (
        <div className="mb-4 grid gap-3">
          {visiblePlans.map((plan) => <PlanCard key={plan.id} plan={plan} editable={lead} onSaved={() => setRefresh((value) => value + 1)} />)}
        </div>
      )}
      <div className="space-y-4">
        {plans.employees.map((employee) => (
          <section key={employee.user.id} className="rounded-2xl border border-line bg-card p-4">
            <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
              <div>
                {lead || viewer ? <Link className="font-display text-xl font-semibold" to={`/team/${employee.user.id}`}>{employee.user.name}</Link> : <div className="font-display text-xl font-semibold">{employee.user.name}</div>}
                <div className="text-xs text-muted">{employee.user.departmentName ?? "No department"} · {employee.plannedTasks} goals · {goalCompletion(employee.completionPercent, employee.plannedTasks)} complete · {employee.blocked} blocked</div>
              </div>
              <div className="flex gap-2">
                <Badge value={employee.sheetStatus} />
                <Link className="rounded-xl border border-line px-3 py-1.5 text-sm font-semibold" to={`/monthly-plan?userId=${employee.user.id}`}>Open plan</Link>
              </div>
            </div>
            {employee.tasks.length === 0 ? <div className="text-sm text-muted">No matching goals.</div> : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[720px] text-left text-sm">
                  <thead className="text-xs text-muted uppercase"><tr><th className="py-1">Goal</th><th>Project</th><th>Status</th><th>Progress</th><th>Target</th><th>Blockers</th></tr></thead>
                  <tbody>
                    {employee.tasks.map((task) => (
                      <tr key={task.id} className="border-t border-line">
                        <td className="py-2 font-medium">{task.title}</td>
                        <td>{task.projectName ?? "—"}</td>
                        <td><Badge value={task.status} /></td>
                        <td>{task.progressPercent}%</td>
                        <td>{task.plannedEndDate}</td>
                        <td className="max-w-xs text-muted">{task.remarks || "—"}</td>
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

function PlanCard({ plan, editable, onSaved }: { plan: DepartmentPlan; editable: boolean; onSaved: () => void }) {
  const toast = useToast();
  const [status, setStatus] = useState(plan.status);
  const [summary, setSummary] = useState(plan.summary ?? "");
  const [risks, setRisks] = useState(plan.risks ?? "");
  const [comments, setComments] = useState(plan.comments ?? "");

  async function save(event: FormEvent) {
    event.preventDefault();
    try {
      await api(`/api/department-plans/${plan.id}`, {
        method: "PUT",
        body: JSON.stringify({ year: plan.year, month: plan.month, departmentId: plan.departmentId, status, summary, risks, comments }),
      });
      toast("Department plan saved.");
      onSaved();
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  return (
    <form className="rounded-2xl border border-line bg-card p-4" onSubmit={save}>
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div className="font-display text-xl font-semibold">{plan.departmentName}</div>
        <Badge value={plan.status} />
      </div>
      <div className="grid gap-3 md:grid-cols-2">
        <Field label="Status">
          <select className={inputClass} value={status} disabled={!editable} onChange={(event) => setStatus(event.target.value)}>
            {planStatuses.map((item) => <option key={item} value={item}>{label(item)}</option>)}
          </select>
        </Field>
        <Field label="Planned activities"><textarea className={`${inputClass} min-h-20`} value={summary} disabled={!editable} onChange={(event) => setSummary(event.target.value)} /></Field>
        <Field label="Risks and blockers"><textarea className={`${inputClass} min-h-20`} value={risks} disabled={!editable} onChange={(event) => setRisks(event.target.value)} /></Field>
        <Field label="Comments"><textarea className={`${inputClass} min-h-20`} value={comments} disabled={!editable} onChange={(event) => setComments(event.target.value)} /></Field>
      </div>
      {editable ? <div className="mt-3 flex justify-end"><Button type="submit">Save department plan</Button></div> : null}
    </form>
  );
}
