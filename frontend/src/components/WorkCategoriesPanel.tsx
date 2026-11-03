import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Badge, Button, Empty, Field, inputClass } from "./ui";
import { api, errorText } from "../services/api";
import type { WorkCategory } from "../types";

export function WorkCategoriesPanel({ departmentId, departmentName, canManage }: { departmentId: string; departmentName: string; canManage: boolean }) {
  const [rows, setRows] = useState<WorkCategory[] | null>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [editing, setEditing] = useState<WorkCategory | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  async function load() {
    const query = departmentId ? `?departmentId=${departmentId}` : "";
    const all = await api<WorkCategory[]>(`/api/work-categories${query}`);
    setRows(all.filter((row) => row.departmentId === departmentId));
  }

  useEffect(() => { if (departmentId) load().catch((err) => setError(errorText(err))); }, [departmentId]);

  async function create(event: FormEvent) {
    event.preventDefault();
    setError("");
    try {
      await api("/api/work-categories", { method: "POST", body: JSON.stringify({ departmentId, name, description }) });
      setName("");
      setDescription("");
      setMessage("Category added. It is available for new work in this department only.");
      await load();
    } catch (err) {
      setError(errorText(err));
    }
  }

  async function saveEdit() {
    if (!editing) return;
    setError("");
    try {
      await api(`/api/work-categories/${editing.id}`, { method: "PUT", body: JSON.stringify({ departmentId, name: editing.name, description: editing.description }) });
      setEditing(null);
      setMessage("Category renamed. Existing projects, initiatives, and goals keep this category.");
      await load();
    } catch (err) {
      setError(errorText(err));
    }
  }

  async function deactivate(row: WorkCategory) {
    if (!window.confirm(`Deactivate ${row.name}? It cannot be chosen for new work. Existing records keep it.`)) return;
    setError("");
    try {
      await api(`/api/work-categories/${row.id}/deactivate`, { method: "POST" });
      setMessage(`${row.name} is inactive.`);
      await load();
    } catch (err) {
      setError(errorText(err));
    }
  }

  async function move(index: number, direction: -1 | 1) {
    if (!rows) return;
    const next = [...rows];
    const target = index + direction;
    if (target < 0 || target >= next.length) return;
    const [item] = next.splice(index, 1);
    next.splice(target, 0, item);
    setRows(next);
    try {
      await api("/api/work-categories/reorder", { method: "POST", body: JSON.stringify({ departmentId, ids: next.map((row) => row.id) }) });
    } catch (err) {
      setError(errorText(err));
      await load();
    }
  }

  if (!departmentId) return null;

  return (
    <section className="mt-6 rounded-2xl border border-line bg-card p-4">
      <div className="mb-3">
        <h2 className="font-display text-lg font-semibold">Work categories</h2>
        <p className="text-sm text-muted">{departmentName} starts with none. Add only the categories this department uses. The same name in another department is a different category.</p>
      </div>
      {message ? <p className="mb-3 text-sm text-emerald-800">{message}</p> : null}
      {error ? <p className="mb-3 text-sm text-rose-700">{error}</p> : null}
      {canManage ? (
        <form className="mb-4 grid gap-3 md:grid-cols-[1fr_1fr_auto] md:items-end" onSubmit={create}>
          <Field label="Category name"><input className={inputClass} value={name} onChange={(event) => setName(event.target.value)} required maxLength={120} /></Field>
          <Field label="Description"><input className={inputClass} value={description} onChange={(event) => setDescription(event.target.value)} maxLength={1000} /></Field>
          <Button type="submit">Add work category</Button>
        </form>
      ) : null}
      {rows && rows.length === 0 ? <Empty title="No work categories yet" body="Create the categories this department needs. Nothing is added automatically." /> : null}
      {rows && rows.length > 0 ? (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[720px] text-left text-sm">
            <thead className="text-xs tracking-wide text-muted uppercase">
              <tr>{["Order", "Name", "Description", "Status", "Created", ""].map((heading) => <th key={heading} className="px-2 py-2">{heading}</th>)}</tr>
            </thead>
            <tbody>
              {rows.map((row, index) => (
                <tr key={row.id} className="border-t border-line">
                  <td className="px-2 py-2">{canManage ? <span className="space-x-1"><button type="button" onClick={() => move(index, -1)} aria-label="Move up">↑</button><button type="button" onClick={() => move(index, 1)} aria-label="Move down">↓</button></span> : row.displayOrder}</td>
                  <td className="px-2 py-2 font-semibold">{editing?.id === row.id ? <input className={inputClass} value={editing.name} onChange={(event) => setEditing({ ...editing, name: event.target.value })} /> : row.name}</td>
                  <td className="px-2 py-2">{editing?.id === row.id ? <input className={inputClass} value={editing.description ?? ""} onChange={(event) => setEditing({ ...editing, description: event.target.value })} /> : row.description ?? "—"}</td>
                  <td className="px-2 py-2">{row.isActive ? <Badge value="Active" /> : "Inactive"}</td>
                  <td className="px-2 py-2 text-muted">{row.createdByName ?? "—"} · {new Date(row.createdAt).toLocaleDateString()}</td>
                  <td className="px-2 py-2 text-right whitespace-nowrap">
                    {canManage && editing?.id === row.id ? <Button type="button" onClick={() => saveEdit()}>Save</Button> : null}
                    {canManage && editing?.id !== row.id ? <button className="mr-3 font-semibold text-pine" onClick={() => setEditing(row)}>Edit</button> : null}
                    {canManage && row.isActive ? <button className="font-semibold text-rose-700" onClick={() => deactivate(row)}>Deactivate</button> : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </section>
  );
}
