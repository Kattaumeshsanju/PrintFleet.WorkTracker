import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { Badge, Button, Field, Loading, Modal, PageHeader, inputClass, useToast } from "../components/ui";
import { api, errorText } from "../services/api";
import type { Department, User } from "../types";
import { label } from "../utils/format";

const blank = { name: "", email: "", password: "", jobTitle: "Software Engineer", role: "TeamMember", departmentId: "", isActive: true };

export function TeamPage() {
  const { user } = useAuth();
  const toast = useToast();
  const admin = user?.role === "Admin";
  const [people, setPeople] = useState<User[] | null>(null);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [departmentId, setDepartmentId] = useState("");
  const [newDepartment, setNewDepartment] = useState("");
  const [includeInactive, setIncludeInactive] = useState(false);
  const [editing, setEditing] = useState<User | null>(null);
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState(blank);
  const [error, setError] = useState("");

  async function load() {
    setPeople(await api<User[]>(`/api/users?includeInactive=${includeInactive}`));
  }

  useEffect(() => { load().catch((err) => setError(errorText(err))); }, [includeInactive]);
  useEffect(() => { api<Department[]>("/api/departments").then(setDepartments).catch(() => undefined); }, []);

  function edit(person: User | null) {
    setEditing(person);
    setForm(person
      ? { name: person.name, email: person.email, password: "", jobTitle: person.jobTitle ?? "", role: person.role, departmentId: person.departmentId ?? "", isActive: person.isActive }
      : { ...blank, departmentId: departmentId || departments[0]?.id || "" });
    setOpen(true);
  }

  async function addDepartment() {
    const name = newDepartment.trim();
    if (!name) return;
    try {
      const created = await api<Department>("/api/departments", { method: "POST", body: JSON.stringify({ name }) });
      setDepartments((current) => [...current, created].sort((a, b) => a.name.localeCompare(b.name)));
      setNewDepartment("");
      toast("Department added.");
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  async function save(event: FormEvent) {
    event.preventDefault();
    const body = JSON.stringify({ ...form, password: form.password || null, departmentId: form.departmentId || null });
    try {
      if (editing) await api(`/api/users/${editing.id}`, { method: "PUT", body });
      else await api("/api/users", { method: "POST", body });
      toast(editing ? "Employee updated." : "Employee added.");
      setOpen(false);
      await load();
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  async function deactivate(person: User) {
    if (!window.confirm(`Remove ${person.name} from the active team? Their history stays.`)) return;
    try {
      await api(`/api/users/${person.id}`, { method: "DELETE" });
      toast("Employee removed from the active team.");
      await load();
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  if (error) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;
  if (!people) return <Loading />;

  return (
    <div>
      <PageHeader title="Team" subtitle={`${people.filter((person) => person.isActive).length} active people`} actions={<Button onClick={() => edit(null)}>Add team member</Button>} />
      {admin ? (
        <div className="mb-4 flex flex-wrap items-end gap-2">
          <Field label="New department"><input className={inputClass} value={newDepartment} onChange={(event) => setNewDepartment(event.target.value)} placeholder="Application" /></Field>
          <Button type="button" variant="secondary" onClick={addDepartment}>Add department</Button>
          <Field label="Show">
            <select className={inputClass} value={departmentId} onChange={(event) => setDepartmentId(event.target.value)}>
              <option value="">All departments</option>
              {departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}
            </select>
          </Field>
        </div>
      ) : null}
      <label className="mb-4 flex items-center gap-2 text-sm"><input type="checkbox" checked={includeInactive} onChange={(event) => setIncludeInactive(event.target.checked)} /> Show removed people</label>
      <div className="overflow-x-auto rounded-2xl border border-line bg-card">
        <table className="w-full min-w-[860px] text-left text-sm">
          <thead className="bg-[#f7f3ea] text-xs tracking-wide text-muted uppercase"><tr>{["Name", "Department", "Email", "Role", "Title", "Status", ""].map((heading) => <th key={heading} className="px-3 py-2">{heading}</th>)}</tr></thead>
          <tbody>
            {people.filter((person) => !departmentId || person.departmentId === departmentId).map((person) => (
              <tr key={person.id} className="border-t border-line">
                <td className="px-3 py-2 font-semibold"><Link to={`/team/${person.id}`}>{person.name}</Link></td>
                <td>{person.departmentName ?? "All departments"}</td>
                <td>{person.email}</td>
                <td>{label(person.role)}</td>
                <td>{person.jobTitle}</td>
                <td><Badge value={person.isActive ? "Active" : "Cancelled"} /></td>
                <td className="px-3 whitespace-nowrap">
                  <button className="mr-3 font-semibold text-pine" onClick={() => edit(person)}>Edit</button>
                  {person.isActive ? <button className="font-semibold text-rose-700" onClick={() => deactivate(person)}>Remove</button> : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Modal open={open} title={editing ? "Edit employee" : "Add team member"} onClose={() => setOpen(false)}>
        <form className="grid gap-3" onSubmit={save}>
          <Field label="Name"><input className={inputClass} value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required /></Field>
          <Field label="Email"><input className={inputClass} type="email" value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} required /></Field>
          <Field label={editing ? "New password (optional)" : "Password"}><input className={inputClass} type="password" value={form.password} onChange={(event) => setForm({ ...form, password: event.target.value })} required={!editing} /></Field>
          <Field label="Job title"><input className={inputClass} value={form.jobTitle} onChange={(event) => setForm({ ...form, jobTitle: event.target.value })} /></Field>
          {admin ? (
            <Field label="Department">
              <select className={inputClass} value={form.departmentId} onChange={(event) => setForm({ ...form, departmentId: event.target.value })} required={form.role !== "Admin"}>
                <option value="">Choose a department</option>
                {departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}
              </select>
            </Field>
          ) : null}
          <Field label="Role">
            <select className={inputClass} value={form.role} onChange={(event) => setForm({ ...form, role: event.target.value })} disabled={form.role === "Admin"}>
              {form.role === "Admin" ? <option value="Admin">Admin</option> : null}
              <option value="TeamMember">Team Member</option>
              {admin ? <option value="TeamLead">Team Lead</option> : null}
            </select>
          </Field>
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.isActive} onChange={(event) => setForm({ ...form, isActive: event.target.checked })} /> Active</label>
          <div className="flex justify-end gap-2"><Button type="button" variant="ghost" onClick={() => setOpen(false)}>Cancel</Button><Button type="submit">Save</Button></div>
        </form>
      </Modal>
    </div>
  );
}
