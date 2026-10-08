import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { api, errorText } from "../services/api";
import type { MonthlyTask, Project } from "../types";
import { PRIORITIES, TASK_STATUSES, label } from "../utils/format";
import { Button, Field, Modal, inputClass, useToast } from "./ui";

type Props = {
  open: boolean;
  sheetId: string;
  month: string;
  projects: Project[];
  editing: MonthlyTask | null;
  onClose: () => void;
  onSaved: () => void;
};

export function TaskModal({ open, sheetId, month, projects, editing, onClose, onSaved }: Props) {
  const toast = useToast();
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [projectId, setProjectId] = useState("");
  const [priority, setPriority] = useState("Medium");
  const [status, setStatus] = useState("Planned");
  const [start, setStart] = useState(`${month}-01`);
  const [end, setEnd] = useState(`${month}-01`);
  const [hours, setHours] = useState("8");
  const [remarks, setRemarks] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open) return;
    const last = new Date(Number(month.slice(0, 4)), Number(month.slice(5, 7)), 0).getDate();
    setTitle(editing?.title ?? "");
    setDescription(editing?.description ?? "");
    setProjectId(editing?.projectId ?? "");
    setPriority(editing?.priority ?? "Medium");
    setStatus(editing?.status ?? "Planned");
    setStart(editing?.plannedStartDate ?? `${month}-01`);
    setEnd(editing?.plannedEndDate ?? `${month}-${String(last).padStart(2, "0")}`);
    setHours(String(editing?.estimatedHours ?? 8));
    setRemarks(editing?.remarks ?? "");
  }, [open, editing, month]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (end < start) {
      toast("Planned end must be on or after the planned start.", "error");
      return;
    }
    setSaving(true);
    const body = JSON.stringify({
      projectId: projectId || null,
      title,
      description,
      priority,
      plannedStartDate: start,
      plannedEndDate: end,
      estimatedHours: Number(hours),
      status,
      remarks,
    });
    try {
      if (editing) await api(`/api/monthly-tasks/${editing.id}`, { method: "PUT", body });
      else await api(`/api/monthly-work/${sheetId}/tasks`, { method: "POST", body });
      toast(editing ? "Task updated." : "Task added.");
      onSaved();
      onClose();
    } catch (error) {
      toast(errorText(error), "error");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal open={open} title={editing ? "Edit planned task" : "Add planned task"} onClose={onClose}>
      <form className="grid gap-3" onSubmit={submit}>
        <Field label="Planned task"><input className={inputClass} value={title} onChange={(event) => setTitle(event.target.value)} required /></Field>
        <Field label="Description"><textarea className={`${inputClass} min-h-20`} value={description} onChange={(event) => setDescription(event.target.value)} /></Field>
        <Field label="Project">
          <select className={inputClass} value={projectId} onChange={(event) => setProjectId(event.target.value)}>
            <option value="">No project</option>
            {projects.map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}
          </select>
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Priority">
            <select className={inputClass} value={priority} onChange={(event) => setPriority(event.target.value)}>
              {PRIORITIES.map((item) => <option key={item}>{item}</option>)}
            </select>
          </Field>
          <Field label="Status">
            <select className={inputClass} value={status} onChange={(event) => setStatus(event.target.value)}>
              {TASK_STATUSES.map((item) => <option key={item} value={item}>{label(item)}</option>)}
            </select>
          </Field>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Planned start"><input className={inputClass} type="date" min={`${month}-01`} max={`${month}-31`} value={start} onChange={(event) => setStart(event.target.value)} required /></Field>
          <Field label="Planned end"><input className={inputClass} type="date" min={`${month}-01`} max={`${month}-31`} value={end} onChange={(event) => setEnd(event.target.value)} required /></Field>
        </div>
        <Field label="Estimated hours"><input className={inputClass} type="number" min="0" step="0.5" value={hours} onChange={(event) => setHours(event.target.value)} required /></Field>
        <Field label="Remarks"><textarea className={`${inputClass} min-h-16`} value={remarks} onChange={(event) => setRemarks(event.target.value)} /></Field>
        <div className="mt-2 flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" disabled={saving}>{saving ? "Saving…" : "Save task"}</Button>
        </div>
      </form>
    </Modal>
  );
}
