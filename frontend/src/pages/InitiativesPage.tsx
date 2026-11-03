import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useAuth } from "../auth/AuthContext";
import { Badge, Button, Field, Loading, Modal, PageHeader, inputClass } from "../components/ui";
import { api, downloadFile, errorText } from "../services/api";
import type { Department, Initiative, Paged, Project, User, WorkCategory } from "../types";
import { PRIORITIES, canManage, canViewOrganization, label } from "../utils/format";

const statuses = ["Planned", "Ongoing", "Completed", "OnHold", "Cancelled"];
const stages = ["", "Idea", "Research", "ProofOfConcept", "Validation", "Pilot", "Completed"];
const blank = { title: "", description: "", departmentId: "", workCategoryId: "", projectId: "", status: "Planned", priority: "Medium", ownerId: "", collaboratorIds: [] as string[], targetStart: "", targetEnd: "", remarks: "", researchObjective: "", hypothesis: "", researchStage: "", successCriteria: "", experimentPlan: "", conclusion: "" };

export function InitiativesPage() {
  const { user } = useAuth();
  const manager = canManage(user?.role);
  const viewer = canViewOrganization(user?.role);
  const [page, setPage] = useState<Paged<Initiative> | null>(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [priority, setPriority] = useState("");
  const [stage, setStage] = useState("");
  const [departmentId, setDepartmentId] = useState("");
  const [departments, setDepartments] = useState<Department[]>([]);
  const [categories, setCategories] = useState<WorkCategory[]>([]);
  const [projects, setProjects] = useState<Project[]>([]);
  const [people, setPeople] = useState<User[]>([]);
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<Initiative | null>(null);
  const [form, setForm] = useState(blank);
  const [detail, setDetail] = useState<Initiative | null>(null);
  const [error, setError] = useState("");
  const [recordTitle, setRecordTitle] = useState("");
  const [findings, setFindings] = useState("");
  const [outcome, setOutcome] = useState("");

  const query = () => {
    const params = new URLSearchParams({ page: String(pageNumber), pageSize: "25" });
    if (search.trim()) params.set("search", search.trim());
    if (status) params.set("status", status);
    if (priority) params.set("priority", priority);
    if (stage) params.set("stage", stage);
    if (departmentId) params.set("departmentId", departmentId);
    return params;
  };

  async function load() {
    setPage(await api<Paged<Initiative>>(`/api/initiatives?${query()}`));
  }

  useEffect(() => { load().catch((err) => setError(errorText(err))); }, [pageNumber, status, priority, stage, departmentId]);
  useEffect(() => {
    if (viewer) api<Department[]>("/api/departments").then(setDepartments).catch(() => undefined);
    api<WorkCategory[]>("/api/work-categories").then(setCategories).catch(() => undefined);
    api<Project[]>("/api/projects").then(setProjects).catch(() => undefined);
    if (manager) api<User[]>("/api/users").then(setPeople).catch(() => undefined);
  }, [viewer, manager]);

  async function startEdit(initiative: Initiative | null) {
    const full = initiative ? await api<Initiative>(`/api/initiatives/${initiative.id}`) : null;
    setEditing(full);
    setForm(initiative ? {
      title: initiative.title,
      description: initiative.description ?? "",
      departmentId: initiative.departmentId,
      workCategoryId: initiative.workCategoryId ?? "",
      projectId: initiative.projectId ?? "",
      status: initiative.status,
      priority: initiative.priority,
      ownerId: initiative.ownerId ?? "",
      collaboratorIds: initiative.collaborators.map((person) => person.id),
      targetStart: initiative.targetStart ?? "",
      targetEnd: initiative.targetEnd ?? "",
      remarks: initiative.remarks ?? "",
      researchObjective: initiative.researchObjective ?? "",
      hypothesis: initiative.hypothesis ?? "",
      researchStage: initiative.researchStage ?? "",
      successCriteria: initiative.successCriteria ?? "",
      experimentPlan: initiative.experimentPlan ?? "",
      conclusion: initiative.conclusion ?? "",
    } : { ...blank, departmentId: departmentId || user?.departmentId || "" });
    setOpen(true);
  }

  async function save(event: FormEvent) {
    event.preventDefault();
    const body = JSON.stringify({
      ...form,
      departmentId: form.departmentId || null,
      workCategoryId: form.workCategoryId || null,
      projectId: form.projectId || null,
      ownerId: form.ownerId || null,
      targetStart: form.targetStart || null,
      targetEnd: form.targetEnd || null,
      researchStage: form.researchStage || null,
    });
    try {
      if (editing) await api(`/api/initiatives/${editing.id}`, { method: "PUT", body });
      else await api("/api/initiatives", { method: "POST", body });
      setOpen(false);
      await load();
    } catch (err) {
      setError(errorText(err));
    }
  }

  async function openDetail(id: string) {
    setDetail(await api<Initiative>(`/api/initiatives/${id}`));
  }

  async function addRecord(event: FormEvent) {
    event.preventDefault();
    if (!detail) return;
    try {
      await api(`/api/initiatives/${detail.id}/records`, { method: "POST", body: JSON.stringify({ title: recordTitle, findings, outcome, recordedOn: new Date().toISOString().slice(0, 10) }) });
      setRecordTitle("");
      setFindings("");
      setOutcome("");
      setDetail(await api<Initiative>(`/api/initiatives/${detail.id}`));
    } catch (err) {
      setError(errorText(err));
    }
  }

  const formDepartment = form.departmentId || user?.departmentId || "";
  const categoryChoices = categories.filter((category) => category.departmentId === formDepartment && (category.isActive || category.id === form.workCategoryId));
  const projectChoices = projects.filter((project) => project.departmentId === formDepartment);
  const memberChoices = people.filter((person) => person.isActive && person.departmentId === formDepartment && person.role !== "Admin" && person.role !== "CEO");

  if (error && !page) return <p className="text-sm text-rose-700">{error}</p>;
  if (!page) return <Loading label="Loading initiatives" />;

  return (
    <div>
      <PageHeader title="R&D initiatives" subtitle="An initiative can link to an existing project. Research stage is separate from status." actions={
        <>
          {manager ? <Button onClick={() => startEdit(null)}>New initiative</Button> : null}
          <Button variant="ghost" onClick={() => downloadFile(`/api/initiatives/export?${query()}`, "initiatives.xlsx").catch((err) => setError(errorText(err)))}>Export Excel</Button>
        </>
      } />
      {error ? <p className="mb-3 text-sm text-rose-700">{error}</p> : null}
      <div className="mb-4 flex flex-wrap gap-2">
        <input className="w-full max-w-xs rounded-xl border border-line bg-white px-3 py-2" placeholder="Search number or title" value={search} onChange={(event) => setSearch(event.target.value)} onKeyDown={(event) => { if (event.key === "Enter") { setPageNumber(1); load(); } }} />
        {viewer ? <select className="rounded-xl border border-line bg-white px-3 py-2" value={departmentId} onChange={(event) => { setDepartmentId(event.target.value); setPageNumber(1); }}><option value="">All departments</option>{departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}</select> : null}
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={status} onChange={(event) => { setStatus(event.target.value); setPageNumber(1); }}><option value="">Any status</option>{statuses.map((item) => <option key={item} value={item}>{label(item)}</option>)}</select>
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={priority} onChange={(event) => { setPriority(event.target.value); setPageNumber(1); }}><option value="">Any priority</option>{PRIORITIES.map((item) => <option key={item}>{item}</option>)}</select>
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={stage} onChange={(event) => { setStage(event.target.value); setPageNumber(1); }}><option value="">Any research stage</option>{stages.filter(Boolean).map((item) => <option key={item} value={item}>{label(item)}</option>)}</select>
      </div>
      <div className="overflow-x-auto rounded-2xl border border-line bg-card">
        <table className="w-full min-w-[980px] text-left text-sm">
          <thead className="bg-slate-50 text-xs tracking-wide text-muted uppercase"><tr>{["Number", "Title", "Department", "Category", "Status", "Stage", "Priority", "Owner", "Target", ""].map((heading) => <th key={heading} className="px-3 py-2">{heading}</th>)}</tr></thead>
          <tbody>
            {page.items.length === 0 ? <tr><td className="px-3 py-6 text-muted" colSpan={10}>No initiatives match these filters.</td></tr> : page.items.map((item) => (
              <tr key={item.id} className="border-t border-line">
                <td className="px-3 py-2 font-mono">{item.number}</td>
                <td className="px-3 py-2 font-semibold">{item.title}<div className="font-normal text-muted">{item.projectName ? `Project: ${item.projectName}` : "No linked project"}</div></td>
                <td>{item.departmentName}</td>
                <td>{item.workCategoryName ?? "—"}</td>
                <td><Badge value={item.status} /></td>
                <td>{item.researchStage ? label(item.researchStage) : "—"}</td>
                <td>{item.priority}</td>
                <td>{item.ownerName ?? "—"}</td>
                <td>{item.targetEnd ?? "—"}</td>
                <td className="px-3 whitespace-nowrap"><button className="mr-3 font-semibold text-pine" onClick={() => openDetail(item.id)}>Open</button>{item.canEdit ? <button className="font-semibold text-pine" onClick={() => startEdit(item)}>Edit</button> : null}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="mt-3 flex items-center justify-between text-sm">
        <span className="text-muted">{page.total} initiatives</span>
        <div className="flex gap-2">
          <Button variant="ghost" disabled={pageNumber <= 1} onClick={() => setPageNumber((value) => value - 1)}>Previous</Button>
          <Button variant="ghost" disabled={pageNumber * page.pageSize >= page.total} onClick={() => setPageNumber((value) => value + 1)}>Next</Button>
        </div>
      </div>
      <Modal open={open} title={editing ? "Edit initiative" : "New initiative"} onClose={() => setOpen(false)}>
        <form className="grid max-h-[70vh] gap-3 overflow-y-auto pr-1" onSubmit={save}>
          <Field label="Title"><input className={inputClass} value={form.title} onChange={(event) => setForm({ ...form, title: event.target.value })} required /></Field>
          <Field label="Description / scope"><textarea className={`${inputClass} min-h-20`} value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} /></Field>
          {viewer ? <Field label="Department"><select className={inputClass} value={form.departmentId} onChange={(event) => setForm({ ...form, departmentId: event.target.value, workCategoryId: "", projectId: "", ownerId: "" })} required><option value="">Choose</option>{departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}</select></Field> : null}
          <Field label="Work category"><select className={inputClass} value={form.workCategoryId} onChange={(event) => setForm({ ...form, workCategoryId: event.target.value })}><option value="">None yet</option>{categoryChoices.map((category) => <option key={category.id} value={category.id}>{category.name}{category.isActive ? "" : " (inactive)"}</option>)}</select></Field>
          <Field label="Existing project"><select className={inputClass} value={form.projectId} onChange={(event) => setForm({ ...form, projectId: event.target.value })}><option value="">No linked project</option>{projectChoices.map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}</select></Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Status"><select className={inputClass} value={form.status} onChange={(event) => setForm({ ...form, status: event.target.value })}>{statuses.map((item) => <option key={item} value={item}>{label(item)}</option>)}</select></Field>
            <Field label="Priority"><select className={inputClass} value={form.priority} onChange={(event) => setForm({ ...form, priority: event.target.value })}>{PRIORITIES.map((item) => <option key={item}>{item}</option>)}</select></Field>
          </div>
          <Field label="Owner"><select className={inputClass} value={form.ownerId} onChange={(event) => setForm({ ...form, ownerId: event.target.value })}><option value="">Unassigned</option>{memberChoices.map((person) => <option key={person.id} value={person.id}>{person.name}</option>)}</select></Field>
          <Field label="Collaborators"><select className={inputClass} multiple value={form.collaboratorIds} onChange={(event) => setForm({ ...form, collaboratorIds: Array.from(event.target.selectedOptions).map((option) => option.value) })}>{memberChoices.filter((person) => person.id !== form.ownerId).map((person) => <option key={person.id} value={person.id}>{person.name}</option>)}</select></Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Target start"><input className={inputClass} type="date" value={form.targetStart} onChange={(event) => setForm({ ...form, targetStart: event.target.value })} /></Field>
            <Field label="Target completion"><input className={inputClass} type="date" value={form.targetEnd} onChange={(event) => setForm({ ...form, targetEnd: event.target.value })} /></Field>
          </div>
          <Field label="Remarks"><textarea className={`${inputClass} min-h-16`} value={form.remarks} onChange={(event) => setForm({ ...form, remarks: event.target.value })} /></Field>
          <h3 className="font-semibold">Research details, if this work needs them</h3>
          <Field label="Research stage"><select className={inputClass} value={form.researchStage} onChange={(event) => setForm({ ...form, researchStage: event.target.value })}>{stages.map((item) => <option key={item || "none"} value={item}>{item ? label(item) : "Not a research workflow"}</option>)}</select></Field>
          <Field label="Objective"><textarea className={`${inputClass} min-h-16`} value={form.researchObjective} onChange={(event) => setForm({ ...form, researchObjective: event.target.value })} /></Field>
          <Field label="Hypothesis or approach"><textarea className={`${inputClass} min-h-16`} value={form.hypothesis} onChange={(event) => setForm({ ...form, hypothesis: event.target.value })} /></Field>
          <Field label="Success criteria"><textarea className={`${inputClass} min-h-16`} value={form.successCriteria} onChange={(event) => setForm({ ...form, successCriteria: event.target.value })} /></Field>
          <Field label="Experiment plan"><textarea className={`${inputClass} min-h-16`} value={form.experimentPlan} onChange={(event) => setForm({ ...form, experimentPlan: event.target.value })} /></Field>
          <Field label="Conclusion and next steps"><textarea className={`${inputClass} min-h-16`} value={form.conclusion} onChange={(event) => setForm({ ...form, conclusion: event.target.value })} /></Field>
          <div className="flex justify-end gap-2"><Button type="button" variant="ghost" onClick={() => setOpen(false)}>Cancel</Button><Button type="submit">Save</Button></div>
        </form>
      </Modal>
      <Modal open={!!detail} title={detail ? `${detail.number} ${detail.title}` : "Initiative"} onClose={() => setDetail(null)}>
        {detail ? (
          <div className="max-h-[70vh] space-y-4 overflow-y-auto pr-1 text-sm">
            <p className="text-muted">{detail.departmentName}{detail.workCategoryName ? ` · ${detail.workCategoryName}` : ""} · {label(detail.status)}{detail.researchStage ? ` · ${label(detail.researchStage)}` : ""}</p>
            <p>{detail.description}</p>
            <div>
              <h3 className="font-semibold">Milestones</h3>
              {detail.milestones.length === 0 ? <p className="text-muted">No milestones yet.</p> : detail.milestones.map((milestone) => (
                <div key={milestone.id} className="mt-2 rounded-xl border border-line px-3 py-2">{milestone.title} · {label(milestone.status)} · {milestone.targetDate ?? "No date"}{milestone.ownerName ? ` · ${milestone.ownerName}` : ""}</div>
              ))}
              {detail.canEdit ? <MilestoneForm initiativeId={detail.id} people={memberChoices} onSaved={() => openDetail(detail.id)} /> : null}
            </div>
            <div>
              <h3 className="font-semibold">Findings and validation</h3>
              <p className="text-muted">New records are kept. Earlier conclusions are not replaced.</p>
              {detail.records.map((record) => (
                <article key={record.id} className="mt-2 rounded-xl bg-slate-50 px-3 py-2">
                  <div className="font-semibold">{record.title} · {record.recordedOn}</div>
                  {record.findings ? <p>{record.findings}</p> : null}
                  {record.outcome ? <p>Outcome: {record.outcome}</p> : null}
                  {record.evidenceLinks ? <p>Evidence: {record.evidenceLinks}</p> : null}
                </article>
              ))}
              {detail.canRecord ? (
                <form className="mt-3 grid gap-2" onSubmit={addRecord}>
                  <Field label="Record title"><input className={inputClass} value={recordTitle} onChange={(event) => setRecordTitle(event.target.value)} required /></Field>
                  <Field label="Findings"><textarea className={`${inputClass} min-h-16`} value={findings} onChange={(event) => setFindings(event.target.value)} /></Field>
                  <Field label="Outcome"><input className={inputClass} value={outcome} onChange={(event) => setOutcome(event.target.value)} /></Field>
                  <Button type="submit">Add research record</Button>
                </form>
              ) : null}
            </div>
          </div>
        ) : null}
      </Modal>
    </div>
  );
}

function MilestoneForm({ initiativeId, people, onSaved }: { initiativeId: string; people: User[]; onSaved: () => void }) {
  const [title, setTitle] = useState("");
  const [targetDate, setTargetDate] = useState("");
  const [ownerId, setOwnerId] = useState("");
  const [error, setError] = useState("");

  async function submit(event: FormEvent) {
    event.preventDefault();
    try {
      await api(`/api/initiatives/${initiativeId}/milestones`, { method: "POST", body: JSON.stringify({ title, targetDate: targetDate || null, ownerId: ownerId || null, status: "Planned" }) });
      setTitle("");
      onSaved();
    } catch (err) {
      setError(errorText(err));
    }
  }

  return (
    <form className="mt-3 grid gap-2" onSubmit={submit}>
      {error ? <p className="text-rose-700">{error}</p> : null}
      <div className="grid gap-2 sm:grid-cols-3">
        <input className={inputClass} placeholder="Milestone" value={title} onChange={(event) => setTitle(event.target.value)} required />
        <input className={inputClass} type="date" value={targetDate} onChange={(event) => setTargetDate(event.target.value)} />
        <select className={inputClass} value={ownerId} onChange={(event) => setOwnerId(event.target.value)}><option value="">Owner</option>{people.map((person) => <option key={person.id} value={person.id}>{person.name}</option>)}</select>
      </div>
      <Button type="submit" variant="secondary">Add milestone</Button>
    </form>
  );
}
