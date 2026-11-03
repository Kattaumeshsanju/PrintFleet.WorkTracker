import { useEffect, useMemo, useState } from "react";
import type { FormEvent } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { Badge, Button, Field, Loading, Modal, PageHeader, inputClass, useToast } from "../components/ui";
import { api, errorText } from "../services/api";
import type { Department, User } from "../types";
import { label } from "../utils/format";

type DraftMember = {
  key: string;
  userId: string | null;
  name: string;
  email: string;
  jobTitle: string;
  role: "TeamLead" | "TeamMember";
  isActive: boolean;
  homeDepartmentId: string | null;
  homeDepartmentName: string | null;
  isNew: boolean;
  password: string;
};

const blankAccount = { name: "", email: "", password: "", jobTitle: "Software Engineer" };

function snapshot(rows: DraftMember[]) {
  return JSON.stringify(rows.map((row) => ({ id: row.userId ?? row.email, role: row.role, isNew: row.isNew, password: row.isNew ? row.password : "" })));
}

export function TeamPage() {
  const { user } = useAuth();
  const toast = useToast();
  const admin = user?.role === "Admin";
  const [departments, setDepartments] = useState<Department[]>([]);
  const [departmentId, setDepartmentId] = useState("");
  const [people, setPeople] = useState<User[] | null>(null);
  const [draft, setDraft] = useState<DraftMember[]>([]);
  const [saved, setSaved] = useState("[]");
  const [saving, setSaving] = useState(false);
  const [addOpen, setAddOpen] = useState(false);
  const [departmentOpen, setDepartmentOpen] = useState(false);
  const [departmentName, setDepartmentName] = useState("");
  const [existingId, setExistingId] = useState("");
  const [account, setAccount] = useState(blankAccount);
  const [formError, setFormError] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [homeDepartment, setHomeDepartment] = useState<Record<string, string>>({});

  const dirty = snapshot(draft) !== saved;
  const department = departments.find((item) => item.id === departmentId);

  async function load(nextDepartmentId = departmentId) {
    const [nextDepartments, nextPeople] = await Promise.all([
      api<Department[]>("/api/departments"),
      api<User[]>("/api/users?includeInactive=true"),
    ]);
    setDepartments(nextDepartments);
    setPeople(nextPeople);
    const selected = nextDepartmentId || nextDepartments.find((item) => item.isActive)?.id || nextDepartments[0]?.id || "";
    setDepartmentId(selected);
    return { nextDepartments, nextPeople, selected };
  }

  useEffect(() => { load().catch((err) => setError(errorText(err))); }, []);

  useEffect(() => {
    if (!people || !departmentId) return;
    const rows = people
      .filter((person) => person.departmentId === departmentId && person.role !== "Admin" && person.role !== "CEO")
      .map((person) => toDraft(person));
    setDraft(rows);
    setSaved(snapshot(rows));
  }, [people, departmentId]);

  useEffect(() => {
    const onLeave = (event: BeforeUnloadEvent) => {
      if (!dirty) return;
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", onLeave);
    return () => window.removeEventListener("beforeunload", onLeave);
  }, [dirty]);

  const candidates = useMemo(() => (people ?? []).filter((person) => person.isActive && person.role !== "Admin" && person.role !== "CEO" && !draft.some((row) => row.userId === person.id)), [people, draft]);
  const executives = (people ?? []).filter((person) => person.role === "CEO");
  const lead = draft.find((row) => row.role === "TeamLead");

  function chooseDepartment(id: string) {
    if (id === departmentId) return;
    if (dirty && !window.confirm("You have unsaved team changes. Leave this department without saving?")) return;
    setMessage("");
    setDepartmentId(id);
  }

  function setRole(key: string, role: "TeamLead" | "TeamMember") {
    setDraft((rows) => rows.map((row) => {
      if (row.key === key) return { ...row, role };
      if (role === "TeamLead" && row.role === "TeamLead") return { ...row, role: "TeamMember" };
      return row;
    }));
  }

  function removeRow(row: DraftMember) {
    const text = row.isNew
      ? `Discard the new account for ${row.name || row.email}?`
      : `Remove ${row.name} from ${department?.name}? The account stays active and can be added to a department later.`;
    if (!window.confirm(text)) return;
    setDraft((rows) => rows.filter((item) => item.key !== row.key));
  }

  function addExisting() {
    const person = candidates.find((item) => item.id === existingId);
    if (!person || !department) return;
    if (person.departmentId && person.departmentId !== department.id) {
      const confirmed = window.confirm(`${person.name} is currently in ${person.departmentName ?? "another department"}. Move them to ${department.name}?`);
      if (!confirmed) return;
    }
    setDraft((rows) => [...rows, toDraft(person)]);
    setExistingId("");
    setAddOpen(false);
    setFormError("");
  }

  function addAccount(event: FormEvent) {
    event.preventDefault();
    const email = account.email.trim().toLowerCase();
    if ((people ?? []).some((person) => person.email.toLowerCase() === email) || draft.some((row) => row.email.toLowerCase() === email)) {
      setFormError("That email already has an account. Add the existing employee instead.");
      return;
    }
    setDraft((rows) => [...rows, {
      key: `new-${email}`,
      userId: null,
      name: account.name.trim(),
      email,
      jobTitle: account.jobTitle.trim(),
      role: "TeamMember",
      isActive: true,
      homeDepartmentId: null,
      homeDepartmentName: null,
      isNew: true,
      password: account.password,
    }]);
    setAccount(blankAccount);
    setAddOpen(false);
    setFormError("");
  }

  async function createDepartment(event: FormEvent) {
    event.preventDefault();
    try {
      const created = await api<Department>("/api/departments", { method: "POST", body: JSON.stringify({ name: departmentName }) });
      setDepartmentName("");
      setDepartmentOpen(false);
      toast("Department created.");
      if (dirty && !window.confirm("You have unsaved team changes. Switch to the new department without saving?")) {
        await load(departmentId);
        return;
      }
      await load(created.id);
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  async function saveTeam() {
    if (!departmentId || saving) return;
    const leads = draft.filter((row) => row.role === "TeamLead");
    if (leads.length > 1) {
      toast("A department can have only one team lead.", "error");
      return;
    }
    setSaving(true);
    try {
      await api(`/api/departments/${departmentId}/team`, {
        method: "PUT",
        body: JSON.stringify({
          members: draft.filter((row) => row.userId).map((row) => ({ userId: row.userId, role: row.role })),
          newMembers: draft.filter((row) => row.isNew).map((row) => ({
            name: row.name,
            email: row.email,
            password: row.password,
            jobTitle: row.jobTitle,
            role: row.role,
          })),
        }),
      });
      toast("Team saved.");
      setMessage("The department team was saved.");
      await load(departmentId);
    } catch (err) {
      toast(errorText(err), "error");
    } finally {
      setSaving(false);
    }
  }

  async function grantCeo(person: User) {
    if (person.id === user?.id) return;
    if (!window.confirm(`Grant ${person.name} read-only CEO access? This is separate from their department role. They leave the department and cannot change users, departments, or settings.`)) return;
    try {
      await api(`/api/users/${person.id}/organization-role`, { method: "POST", body: JSON.stringify({ role: "CEO" }) });
      toast("CEO access granted. They need to sign in again.");
      await load(departmentId);
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  async function returnToDepartment(person: User) {
    const target = homeDepartment[person.id];
    if (!target) return;
    const name = departments.find((item) => item.id === target)?.name ?? "that department";
    if (!window.confirm(`Return ${person.name} to ${name} as an employee? CEO access will be removed.`)) return;
    try {
      await api(`/api/users/${person.id}/organization-role`, { method: "POST", body: JSON.stringify({ role: "TeamMember", departmentId: target }) });
      toast(`${person.name} is an employee again.`);
      await load(departmentId);
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  if (error) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;
  if (!people) return <Loading />;

  return (
    <div>
      <PageHeader
        title="Team"
        subtitle="One department, one team. Change roles here, then save the whole team together."
        actions={
          <div className="flex flex-wrap gap-2">
            {admin ? <Button variant="secondary" onClick={() => setDepartmentOpen(true)}>Add department</Button> : null}
            <Button onClick={() => { setFormError(""); setAddOpen(true); }} disabled={!departmentId || department?.isActive === false}>Add member</Button>
            <Button onClick={saveTeam} disabled={!dirty || saving || department?.isActive === false}>{saving ? "Saving..." : "Save changes"}</Button>
          </div>
        }
      />
      <div className="mb-4 grid gap-3 md:grid-cols-2">
        <Field label="Department">
          <select className={inputClass} value={departmentId} onChange={(event) => chooseDepartment(event.target.value)}>
            {departments.map((item) => <option key={item.id} value={item.id}>{item.name}{item.isActive ? "" : " (inactive)"}</option>)}
          </select>
        </Field>
        <div className="rounded-2xl border border-line bg-card px-4 py-3 text-sm">
          <div>Team lead: <strong>{lead?.name ?? department?.teamLeadName ?? "Not assigned yet"}</strong></div>
          <div className="text-muted">{department?.memberCount ?? 0} active people · {department?.activeProjectCount ?? 0} active projects</div>
        </div>
      </div>
      {dirty ? <div className="mb-4 rounded-2xl bg-amber-50 px-4 py-3 text-sm text-amber-950">You have unsaved changes. Nothing is stored until Save changes succeeds.</div> : null}
      {message && !dirty ? <div className="mb-4 rounded-2xl bg-emerald-50 px-4 py-3 text-sm text-emerald-900">{message}</div> : null}
      {department?.isActive === false ? <div className="mb-4 rounded-2xl bg-stone-100 px-4 py-3 text-sm">This department is inactive. Activate it before changing the team.</div> : null}

      {draft.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-line bg-card px-4 py-10 text-center">
          <div className="font-display text-xl font-semibold">No members yet</div>
          <p className="mx-auto mt-1 max-w-md text-sm text-muted">Add the people who belong to {department?.name ?? "this department"}. A new department can be saved before it has a team lead.</p>
          <Button className="mt-4" onClick={() => setAddOpen(true)} disabled={department?.isActive === false}>Add members</Button>
        </div>
      ) : (
        <>
          <div className="space-y-3 md:hidden">
            {draft.map((row) => <MemberCard key={row.key} row={row} admin={admin} removable={row.userId !== user?.id} onRole={setRole} onRemove={removeRow} />)}
          </div>
          <div className="hidden overflow-x-auto rounded-2xl border border-line bg-card md:block">
            <table className="w-full min-w-[720px] text-left text-sm">
              <thead className="bg-slate-50 text-xs tracking-wide text-muted uppercase">
                <tr>{["Employee", "Email", "Job title", "Department role", "Account status", ""].map((heading) => <th key={heading} className="px-3 py-2">{heading}</th>)}</tr>
              </thead>
              <tbody>
                {draft.map((row) => (
                  <tr key={row.key} className={`border-t border-line ${row.role === "TeamLead" ? "bg-[#fbf6ea]" : ""}`}>
                    <td className="px-3 py-2 font-semibold">
                      {row.userId ? <Link to={`/team/${row.userId}`}>{row.name}</Link> : row.name}
                      {row.isNew ? <div className="text-xs font-normal text-pine">New account, saved with the team</div> : null}
                      {row.homeDepartmentId && row.homeDepartmentId !== departmentId ? <div className="text-xs font-normal text-amber-800">Moving from {row.homeDepartmentName}</div> : null}
                    </td>
                    <td className="px-3 py-2">{row.email}</td>
                    <td className="px-3 py-2">{row.jobTitle || "—"}</td>
                    <td className="px-3 py-2">
                      {admin ? (
                        <select className={inputClass} value={row.role} onChange={(event) => setRole(row.key, event.target.value as "TeamLead" | "TeamMember")}>
                          <option value="TeamLead">Team Lead</option>
                          <option value="TeamMember">Employee</option>
                        </select>
                      ) : <span>{label(row.role)}</span>}
                    </td>
                    <td className="px-3 py-2"><Badge value={row.isActive ? "Active" : "Cancelled"} /></td>
                    <td className="px-3 py-2">{row.userId !== user?.id ? <button className="font-semibold text-rose-700" onClick={() => removeRow(row)}>Remove</button> : null}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      {admin ? (
        <div className="mt-4 rounded-2xl border border-line bg-card p-4">
          <div className="font-display text-lg font-semibold">Organization access</div>
          <p className="mb-3 text-sm text-muted">CEO access is separate from a department role. Only an admin can grant it, and nobody can grant it to themselves. A CEO can review every department and cannot change users, departments, or settings.</p>
          <Field label="Grant CEO access">
            <div className="flex flex-wrap items-end gap-2">
              <select className={inputClass} value="" onChange={(event) => {
                const person = (people ?? []).find((item) => item.id === event.target.value);
                if (person) grantCeo(person);
              }}>
                <option value="">Choose an employee</option>
                {(people ?? []).filter((person) => person.isActive && person.id !== user?.id && person.role !== "Admin" && person.role !== "CEO").map((person) => (
                  <option key={person.id} value={person.id}>{person.name} · {person.departmentName ?? "No department"} · {label(person.role)}</option>
                ))}
              </select>
            </div>
          </Field>
          {executives.length === 0 ? <div className="mt-3 text-sm text-muted">No CEO account is assigned.</div> : executives.map((person) => (
            <div key={person.id} className="mt-3 flex flex-wrap items-end gap-2">
              <div className="text-sm font-semibold">{person.name}<div className="font-normal text-muted">{person.email}</div></div>
              <select className={inputClass} value={homeDepartment[person.id] ?? ""} onChange={(event) => setHomeDepartment({ ...homeDepartment, [person.id]: event.target.value })}>
                <option value="">Return to a department</option>
                {departments.filter((item) => item.isActive).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
              </select>
              <Button type="button" variant="secondary" disabled={!homeDepartment[person.id]} onClick={() => returnToDepartment(person)}>Return as employee</Button>
            </div>
          ))}
        </div>
      ) : null}

      <Modal open={addOpen} title="Add member" onClose={() => setAddOpen(false)}>
        <div className="grid gap-4">
          {admin ? (
            <div className="grid gap-2">
              <Field label="Existing employee">
                <select className={inputClass} value={existingId} onChange={(event) => setExistingId(event.target.value)}>
                  <option value="">Choose a person</option>
                  {candidates.map((person) => <option key={person.id} value={person.id}>{person.name} · {person.departmentName ?? "No department"} · {person.email}</option>)}
                </select>
              </Field>
              <div className="flex justify-end"><Button type="button" variant="secondary" disabled={!existingId} onClick={addExisting}>Add to this team</Button></div>
            </div>
          ) : <p className="text-sm text-muted">People from another department can be moved only by an admin.</p>}
          <form className="grid gap-3 border-t border-line pt-4" onSubmit={addAccount}>
            <div className="text-sm font-semibold">New account</div>
            <p className="text-sm text-muted">This account is created only when you save the team. It starts as an employee.</p>
            {formError ? <div className="rounded-xl bg-rose-50 px-3 py-2 text-sm text-rose-800">{formError}</div> : null}
            <Field label="Name"><input className={inputClass} value={account.name} onChange={(event) => setAccount({ ...account, name: event.target.value })} required /></Field>
            <Field label="Email"><input className={inputClass} type="email" value={account.email} onChange={(event) => setAccount({ ...account, email: event.target.value })} required /></Field>
            <Field label="Password"><input className={inputClass} type="password" value={account.password} onChange={(event) => setAccount({ ...account, password: event.target.value })} required minLength={8} /></Field>
            <Field label="Job title"><input className={inputClass} value={account.jobTitle} onChange={(event) => setAccount({ ...account, jobTitle: event.target.value })} /></Field>
            <div className="flex justify-end"><Button type="submit">Add to the list</Button></div>
          </form>
        </div>
      </Modal>

      <Modal open={departmentOpen} title="Add department" onClose={() => setDepartmentOpen(false)}>
        <form className="grid gap-3" onSubmit={createDepartment}>
          <Field label="Department name"><input className={inputClass} value={departmentName} onChange={(event) => setDepartmentName(event.target.value)} required maxLength={120} /></Field>
          <div className="flex justify-end gap-2"><Button type="button" variant="ghost" onClick={() => setDepartmentOpen(false)}>Cancel</Button><Button type="submit">Create</Button></div>
        </form>
      </Modal>
    </div>
  );
}

function toDraft(person: User): DraftMember {
  return {
    key: person.id,
    userId: person.id,
    name: person.name,
    email: person.email,
    jobTitle: person.jobTitle ?? "",
    role: person.role === "TeamLead" ? "TeamLead" : "TeamMember",
    isActive: person.isActive,
    homeDepartmentId: person.departmentId,
    homeDepartmentName: person.departmentName,
    isNew: false,
    password: "",
  };
}

function MemberCard({ row, admin, removable, onRole, onRemove }: { row: DraftMember; admin: boolean; removable: boolean; onRole: (key: string, role: "TeamLead" | "TeamMember") => void; onRemove: (row: DraftMember) => void }) {
  return (
    <section className={`rounded-2xl border border-line bg-card p-3 ${row.role === "TeamLead" ? "bg-[#fbf6ea]" : ""}`}>
      <div className="font-semibold">{row.name}</div>
      <div className="text-sm text-muted">{row.email}</div>
      <div className="text-sm text-muted">{row.jobTitle || "No job title"}</div>
      <div className="mt-2 flex items-center justify-between gap-2">
        {admin ? (
          <select className={inputClass} value={row.role} onChange={(event) => onRole(row.key, event.target.value as "TeamLead" | "TeamMember")}>
            <option value="TeamLead">Team Lead</option>
            <option value="TeamMember">Employee</option>
          </select>
        ) : <Badge value={row.role} />}
        <Badge value={row.isActive ? "Active" : "Cancelled"} />
      </div>
      {removable ? <button className="mt-2 font-semibold text-rose-700" onClick={() => onRemove(row)}>Remove</button> : null}
    </section>
  );
}
