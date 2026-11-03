import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { Badge, Button, Field, Loading, Modal, PageHeader, inputClass, useToast } from "../components/ui";
import { api, errorText } from "../services/api";
import type { Department, Project, User, WorkCategory } from "../types";
import { PRIORITIES, PROJECT_STATUSES, canManage, canViewOrganization, label } from "../utils/format";

const empty = { name: "", code: "", description: "", startDate: "", endDate: "", status: "Active", priority: "Medium", ownerId: "", departmentId: "", workCategoryId: "" };

export function ProjectsPage() {
  const { user } = useAuth();
  const admin = user?.role === "Admin";
  const manager = canManage(user?.role);
  const viewer = canViewOrganization(user?.role);
  const toast = useToast();
  const [params, setParams] = useSearchParams();
  const departmentId = params.get("departmentId") ?? "";
  const [projects, setProjects] = useState<Project[] | null>(null);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [people, setPeople] = useState<User[]>([]);
  const [categories, setCategories] = useState<WorkCategory[]>([]);
  const [search, setSearch] = useState(params.get("search") ?? "");
  const [editing, setEditing] = useState<Project | null>(null);
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState(empty);
  const [error, setError] = useState("");
  const title = viewer ? "Projects" : manager ? "Department Projects" : "My Projects";

  useEffect(() => {
    if (viewer) api<Department[]>("/api/departments").then(setDepartments).catch(() => undefined);
    if (manager) api<User[]>("/api/users").then(setPeople).catch(() => undefined);
    api<WorkCategory[]>("/api/work-categories").then(setCategories).catch(() => undefined);
  }, [viewer, manager]);

  useEffect(() => {
    const query = new URLSearchParams();
    if (search.trim()) query.set("search", search.trim());
    if (viewer && departmentId) query.set("departmentId", departmentId);
    const suffix = query.toString() ? `?${query}` : "";
    api<Project[]>(`/api/projects${suffix}`).then(setProjects).catch((err) => setError(errorText(err)));
  }, [search, departmentId, viewer]);

  function setDepartment(value: string) {
    const next = new URLSearchParams(params);
    if (value) next.set("departmentId", value);
    else next.delete("departmentId");
    if (search.trim()) next.set("search", search.trim());
    setParams(next, { replace: true });
  }

  function edit(project: Project | null) {
    setEditing(project);
    setForm(project ? {
      name: project.name,
      code: project.code,
      description: project.description ?? "",
      startDate: project.startDate ?? "",
      endDate: project.endDate ?? "",
      status: project.status,
      priority: project.priority || "Medium",
      ownerId: project.ownerId ?? "",
      departmentId: project.departmentId ?? "",
      workCategoryId: project.workCategoryId ?? "",
    } : { ...empty, departmentId: departmentId || user?.departmentId || departments[0]?.id || "" });
    setOpen(true);
  }

  async function save(event: FormEvent) {
    event.preventDefault();
    const body = JSON.stringify({
      ...form,
      startDate: form.startDate || null,
      endDate: form.endDate || null,
      ownerId: form.ownerId || null,
      departmentId: admin ? form.departmentId || null : user?.departmentId ?? null,
      workCategoryId: form.workCategoryId || null,
    });
    try {
      if (editing) await api(`/api/projects/${editing.id}`, { method: "PUT", body });
      else await api("/api/projects", { method: "POST", body });
      toast(editing ? "Project updated." : "Project created.");
      setOpen(false);
      const query = new URLSearchParams();
      if (search.trim()) query.set("search", search.trim());
      if (viewer && departmentId) query.set("departmentId", departmentId);
      setProjects(await api<Project[]>(`/api/projects${query.toString() ? `?${query}` : ""}`));
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  async function remove(project: Project) {
    if (!window.confirm(`Delete ${project.name}?`)) return;
    try {
      await api(`/api/projects/${project.id}`, { method: "DELETE" });
      toast("Project deleted.");
      setProjects((current) => current?.filter((item) => item.id !== project.id) ?? []);
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  const ownerDepartment = form.departmentId || user?.departmentId || "";
  const owners = people.filter((person) => person.isActive && person.departmentId === ownerDepartment && person.role !== "Admin" && person.role !== "CEO");

  if (error) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;
  if (!projects) return <Loading />;

  return (
    <div>
      <PageHeader title={title} subtitle={viewer ? "Every department keeps its own projects. Switch the department without leaving this page." : "These are the projects for your department."} actions={manager ? <Button onClick={() => edit(null)}>Add project</Button> : undefined} />
      <div className="mb-4 flex flex-wrap gap-2">
        <input className="w-full max-w-sm rounded-xl border border-line bg-white px-3 py-2" placeholder="Search projects" value={search} onChange={(event) => setSearch(event.target.value)} />
        {viewer ? (
          <select className="rounded-xl border border-line bg-white px-3 py-2" value={departmentId} onChange={(event) => setDepartment(event.target.value)}>
            <option value="">All departments</option>
            {departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}
          </select>
        ) : <div className="rounded-xl border border-line bg-white px-3 py-2 text-sm">{user?.departmentName ?? "Your department"}</div>}
      </div>
      <div className="overflow-x-auto rounded-2xl border border-line bg-card">
        <table className="w-full min-w-[980px] text-left text-sm">
          <thead className="bg-slate-50 text-xs tracking-wide text-muted uppercase"><tr>{["Name", "Department", "Category", "Owner", "Priority", "Code", "Start", "Target", "Status", ""].map((heading) => <th key={heading} className="px-3 py-2">{heading}</th>)}</tr></thead>
          <tbody>
            {projects.length === 0 ? <tr><td className="px-3 py-6 text-muted" colSpan={10}>No projects in this view.</td></tr> : projects.map((project) => (
              <tr key={project.id} className="border-t border-line">
                <td className="px-3 py-2 font-semibold">{project.name}<div className="max-w-xs font-normal text-muted">{project.description}</div></td>
                <td>{project.departmentName ?? "—"}</td>
                <td>{project.workCategoryName ?? "—"}</td>
                <td>{project.ownerName ?? "—"}</td>
                <td>{project.priority}</td>
                <td className="font-mono">{project.code}</td>
                <td>{project.startDate ?? "—"}</td>
                <td>{project.endDate ?? "—"}</td>
                <td><Badge value={project.status} /></td>
                <td className="px-3 whitespace-nowrap">
                  {manager ? <button className="mr-3 font-semibold text-pine" onClick={() => edit(project)}>Edit</button> : null}
                  {manager ? <button className="font-semibold text-rose-700" onClick={() => remove(project)}>Delete</button> : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Modal open={open} title={editing ? "Edit project" : "Add project"} onClose={() => setOpen(false)}>
        <form className="grid gap-3" onSubmit={save}>
          <Field label="Project name"><input className={inputClass} value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required /></Field>
          <Field label="Project code"><input className={inputClass} value={form.code} onChange={(event) => setForm({ ...form, code: event.target.value })} required /></Field>
          <Field label="Description"><textarea className={`${inputClass} min-h-20`} value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} /></Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Start"><input className={inputClass} type="date" value={form.startDate} onChange={(event) => setForm({ ...form, startDate: event.target.value })} /></Field>
            <Field label="Target completion"><input className={inputClass} type="date" value={form.endDate} onChange={(event) => setForm({ ...form, endDate: event.target.value })} /></Field>
          </div>
          {admin ? (
            <Field label="Department">
              <select className={inputClass} value={form.departmentId} onChange={(event) => setForm({ ...form, departmentId: event.target.value, ownerId: "" })} required>
                <option value="">Choose a department</option>
                {departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}
              </select>
            </Field>
          ) : null}
          <Field label="Work category">
            <select className={inputClass} value={form.workCategoryId} onChange={(event) => setForm({ ...form, workCategoryId: event.target.value })}>
              <option value="">No category</option>
              {categories.filter((category) => category.departmentId === ownerDepartment && (category.isActive || category.id === form.workCategoryId)).map((category) => <option key={category.id} value={category.id}>{category.name}{category.isActive ? "" : " (inactive)"}</option>)}
            </select>
          </Field>
          <Field label="Owner">
            <select className={inputClass} value={form.ownerId} onChange={(event) => setForm({ ...form, ownerId: event.target.value })}>
              <option value="">No owner yet</option>
              {owners.map((person) => <option key={person.id} value={person.id}>{person.name}</option>)}
            </select>
          </Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Priority">
              <select className={inputClass} value={form.priority} onChange={(event) => setForm({ ...form, priority: event.target.value })}>
                {PRIORITIES.map((priority) => <option key={priority}>{priority}</option>)}
              </select>
            </Field>
            <Field label="Status">
              <select className={inputClass} value={form.status} onChange={(event) => setForm({ ...form, status: event.target.value })}>
                {PROJECT_STATUSES.map((status) => <option key={status} value={status}>{label(status)}</option>)}
              </select>
            </Field>
          </div>
          <div className="flex justify-end gap-2"><Button type="button" variant="ghost" onClick={() => setOpen(false)}>Cancel</Button><Button type="submit">Save</Button></div>
        </form>
      </Modal>
    </div>
  );
}
