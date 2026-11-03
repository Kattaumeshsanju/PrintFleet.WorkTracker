import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Badge, Button, Field, Loading, Modal, PageHeader, inputClass, useToast } from "../components/ui";
import { WorkCategoriesPanel } from "../components/WorkCategoriesPanel";
import { api, errorText } from "../services/api";
import type { Department } from "../types";

export function DepartmentsPage() {
  const toast = useToast();
  const [departments, setDepartments] = useState<Department[] | null>(null);
  const [name, setName] = useState("");
  const [editing, setEditing] = useState<Department | null>(null);
  const [error, setError] = useState("");
  const [selected, setSelected] = useState("");

  async function load() {
      setDepartments(await api<Department[]>("/api/departments"));
  }

  useEffect(() => { load().catch((err) => setError(errorText(err))); }, []);

  async function create(event: FormEvent) {
    event.preventDefault();
    try {
      await api("/api/departments", { method: "POST", body: JSON.stringify({ name }) });
      setName("");
      toast("Department created.");
      await load();
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  async function rename(event: FormEvent) {
    event.preventDefault();
    if (!editing) return;
    try {
      await api(`/api/departments/${editing.id}`, { method: "PUT", body: JSON.stringify({ name: editing.name }) });
      toast("Department renamed.");
      setEditing(null);
      await load();
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  async function setStatus(department: Department, isActive: boolean) {
    const warning = isActive
      ? `Reactivate ${department.name}?`
      : `Deactivate ${department.name}? This is blocked while it still has active people or projects.`;
    if (!window.confirm(warning)) return;
    try {
      await api(`/api/departments/${department.id}/status`, { method: "POST", body: JSON.stringify({ isActive }) });
      toast(isActive ? "Department reactivated." : "Department deactivated.");
      await load();
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  if (error) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;
  if (!departments) return <Loading />;

  return (
    <div>
      <PageHeader title="Departments" subtitle="Create a department, then assign its team lead and members on the Team screen." />
      <form className="mb-4 flex flex-wrap items-end gap-2" onSubmit={create}>
        <Field label="Department name"><input className={inputClass} value={name} onChange={(event) => setName(event.target.value)} required maxLength={120} placeholder="Application" /></Field>
        <Button type="submit">Create department</Button>
      </form>
      <div className="overflow-x-auto rounded-2xl border border-line bg-card">
        <table className="w-full min-w-[860px] text-left text-sm">
          <thead className="bg-slate-50 text-xs tracking-wide text-muted uppercase">
            <tr>{["Department", "Team lead", "Members", "Active projects", "Status", ""].map((heading) => <th key={heading} className="px-3 py-2">{heading}</th>)}</tr>
          </thead>
          <tbody>
            {departments.map((department) => (
              <tr key={department.id} className="border-t border-line">
                <td className="px-3 py-2 font-semibold">{department.name}</td>
                <td>{department.teamLeadName ?? "Not assigned"}</td>
                <td>{department.memberCount}</td>
                <td>{department.activeProjectCount}</td>
                <td><Badge value={department.isActive ? "Active" : "Cancelled"} /></td>
                <td className="px-3 whitespace-nowrap">
                  <button className="mr-3 font-semibold text-pine" onClick={() => setEditing(department)}>Rename</button>
                  <button className="font-semibold text-rose-700" onClick={() => setStatus(department, !department.isActive)}>{department.isActive ? "Deactivate" : "Reactivate"}</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <section className="mt-6">
        <Field label="Manage categories for">
          <select className={inputClass} value={selected} onChange={(event) => setSelected(event.target.value)}>
            <option value="">Choose a department</option>
            {departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}
          </select>
        </Field>
        {selected ? <WorkCategoriesPanel departmentId={selected} departmentName={departments.find((department) => department.id === selected)?.name ?? ""} canManage /> : null}
      </section>
      <Modal open={!!editing} title="Rename department" onClose={() => setEditing(null)}>
        <form className="grid gap-3" onSubmit={rename}>
          <Field label="Name"><input className={inputClass} value={editing?.name ?? ""} onChange={(event) => setEditing(editing ? { ...editing, name: event.target.value } : editing)} required maxLength={120} /></Field>
          <div className="flex justify-end gap-2"><Button type="button" variant="ghost" onClick={() => setEditing(null)}>Cancel</Button><Button type="submit">Save</Button></div>
        </form>
      </Modal>
    </div>
  );
}
