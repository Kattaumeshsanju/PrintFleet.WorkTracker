import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { api, errorText } from "../services/api";
import type { DailyEntry, MonthlyTask } from "../types";
import { ENTRY_STATUSES, rangesOverlap } from "../utils/format";
import { Button, Field, Modal, inputClass, useToast } from "./ui";
import { label } from "../utils/format";

type Props = {
  open: boolean;
  date: string;
  tasks: MonthlyTask[];
  entries: DailyEntry[];
  editing: DailyEntry | null;
  onClose: () => void;
  onSaved: () => void;
};

export function WorkEntryModal({ open, date, tasks, entries, editing, onClose, onSaved }: Props) {
  const toast = useToast();
  const [taskId, setTaskId] = useState("");
  const [startTime, setStartTime] = useState("09:00");
  const [endTime, setEndTime] = useState("10:00");
  const [description, setDescription] = useState("");
  const [status, setStatus] = useState("InProgress");
  const [remarks, setRemarks] = useState("");
  const [allowOverlap, setAllowOverlap] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open) return;
    setTaskId(editing?.monthlyTaskId ?? tasks[0]?.id ?? "");
    setStartTime(editing?.startTime ?? "09:00");
    setEndTime(editing?.endTime ?? "10:00");
    setDescription(editing?.description ?? "");
    setStatus(editing?.status ?? "InProgress");
    setRemarks(editing?.remarks ?? "");
    setAllowOverlap(false);
  }, [open, editing, tasks]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!taskId) {
      toast("Add a monthly task before recording work.", "error");
      return;
    }
    if (endTime <= startTime) {
      toast("End time must be after start time.", "error");
      return;
    }
    const clash = entries.some((entry) => entry.id !== editing?.id && rangesOverlap(startTime, endTime, entry.startTime, entry.endTime));
    if (clash && !allowOverlap) {
      toast("Time entries overlap. Please correct the timeline.", "error");
      return;
    }
    setSaving(true);
    const body = JSON.stringify({ workDate: date, monthlyTaskId: taskId, startTime, endTime, description, status, remarks, allowOverlap });
    try {
      if (editing) await api(`/api/daily-work/${editing.id}`, { method: "PUT", body });
      else await api("/api/daily-work", { method: "POST", body });
      toast(editing ? "Work entry updated." : "Work entry saved.");
      onSaved();
      onClose();
    } catch (error) {
      toast(errorText(error), "error");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal open={open} title={editing ? "Edit work entry" : "What did you work on?"} onClose={onClose}>
      <form className="grid gap-3" onSubmit={submit}>
        <Field label="Date"><input className={inputClass} value={date} readOnly /></Field>
        <Field label="Monthly task">
          <select className={inputClass} value={taskId} onChange={(event) => setTaskId(event.target.value)} required>
            {tasks.length === 0 ? <option value="">No tasks this month</option> : null}
            {tasks.map((task) => <option key={task.id} value={task.id}>{task.title}</option>)}
          </select>
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Start time"><input className={inputClass} type="time" value={startTime} onChange={(event) => setStartTime(event.target.value)} required /></Field>
          <Field label="End time"><input className={inputClass} type="time" value={endTime} onChange={(event) => setEndTime(event.target.value)} required /></Field>
        </div>
        <Field label="What did you do?">
          <textarea className={`${inputClass} min-h-24`} value={description} onChange={(event) => setDescription(event.target.value)} required />
        </Field>
        <Field label="Status">
          <select className={inputClass} value={status} onChange={(event) => setStatus(event.target.value)}>
            {ENTRY_STATUSES.map((item) => <option key={item} value={item}>{label(item)}</option>)}
          </select>
        </Field>
        <Field label="Remarks">
          <textarea className={`${inputClass} min-h-16`} value={remarks} onChange={(event) => setRemarks(event.target.value)} />
        </Field>
        <label className="flex items-center gap-2 text-sm text-muted">
          <input type="checkbox" checked={allowOverlap} onChange={(event) => setAllowOverlap(event.target.checked)} />
          Allow this entry to overlap another time range
        </label>
        <div className="mt-2 flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" disabled={saving || tasks.length === 0}>{saving ? "Saving…" : "Save"}</Button>
        </div>
      </form>
    </Modal>
  );
}
