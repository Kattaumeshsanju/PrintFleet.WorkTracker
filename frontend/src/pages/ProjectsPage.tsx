import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Badge, Button, Field, Loading, Modal, PageHeader, inputClass, useToast } from "../components/ui";
import { api, errorText } from "../services/api";
import type { Project } from "../types";
import { PROJECT_STATUSES, label } from "../utils/format";

const empty = { name: "", code: "", description: "", startDate: "", endDate: "", status: "Active" };

export function ProjectsPage() {
  const toast = useToast();
  const [projects, setProjects] = useState<Project[] | null>(null);
  const [search, setSearch] = useState("");
  const [editing, setEditing] = useState<Project | null>(null);
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState(empty);
  const [error, setError] = useState("");

  async function load() {
    const query = search.trim() ? `?search=${encodeURIComponent(search.trim())}` : "";
    setProjects(await api<Project[]>(`/api/projects${query}`));
  }

  useEffect(() => { load().catch((err) => setError(errorText(err))); }, [search]);

  function edit(project: Project | null) {
    setEditing(project);
    setForm(project ? {
      name: project.name,
      code: project.code,
      description: project.description ?? "",
      startDate: project.startDate ?? "",
      endDate: project.endDate ?? "",
      status: project.status,
    } : empty);
    setOpen(true);
  }

  async function save(event: FormEvent) {
    event.preventDefault();
    const body = JSON.stringify({ ...form, startDate: form.startDate || null, endDate: form.endDate || null });
    try {
      if (editing) await api(`/api/projects/${editing.id}`, { method: "PUT", body });
      else await api("/api/projects", { method: "POST", body });
      toast(editing ? "Project updated." : "Project created.");
      setOpen(false);
      await load();
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  async function remove(project: Project) {
    if (!window.confirm(`Delete ${project.name}?`)) return;
    try {
      await api(`/api/projects/${project.id}`, { method: "DELETE" });
      toast("Project deleted.");
      await load();
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  if (error) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;
  if (!projects) return <Loading />;

  return (
    <div>
      <PageHeader title="Projects" subtitle="Tasks are linked to these projects. Nothing here is hard-coded." actions={<Button onClick={() => edit(null)}>Add project</Button>} />
      <input className="mb-4 w-full max-w-sm rounded-xl border border-line bg-white px-3 py-2" placeholder="Search projects" value={search} onChange={(event) => setSearch(event.target.value)} />
      <div className="overflow-x-auto rounded-2xl border border-line bg-card">
        <table className="w-full min-w-[760px] text-left text-sm">
          <thead className="bg-[#f7f3ea] text-xs tracking-wide text-muted uppercase"><tr>{["Name", "Code", "Description", "Start", "End", "Status", ""].map((heading) => <th key={heading} className="px-3 py-2">{heading}</th>)}</tr></thead>
          <tbody>
            {projects.map((project) => (
              <tr key={project.id} className="border-t border-line">
                <td className="px-3 py-2 font-semibold">{project.name}</td>
                <td className="font-mono">{project.code}</td>
                <td className="max-w-sm text-muted">{project.description}</td>
                <td>{project.startDate ?? "—"}</td>
                <td>{project.endDate ?? "—"}</td>
                <td><Badge value={project.status} /></td>
                <td className="px-3 whitespace-nowrap">
                  <button className="mr-3 font-semibold text-pine" onClick={() => edit(project)}>Edit</button>
                  <button className="font-semibold text-rose-700" onClick={() => remove(project)}>Delete</button>
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
            <Field label="End"><input className={inputClass} type="date" value={form.endDate} onChange={(event) => setForm({ ...form, endDate: event.target.value })} /></Field>
          </div>
          <Field label="Status">
            <select className={inputClass} value={form.status} onChange={(event) => setForm({ ...form, status: event.target.value })}>
              {PROJECT_STATUSES.map((status) => <option key={status} value={status}>{label(status)}</option>)}
            </select>
          </Field>
          <div className="flex justify-end gap-2"><Button type="button" variant="ghost" onClick={() => setOpen(false)}>Cancel</Button><Button type="submit">Save</Button></div>
        </form>
      </Modal>
    </div>
  );
}
