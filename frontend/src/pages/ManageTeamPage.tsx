import { useEffect, useMemo, useState } from "react";
import type { FormEvent } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { Badge, Button, Field, Loading, Modal, PageHeader, Stat, inputClass } from "../components/ui";
import { WorkCategoriesPanel } from "../components/WorkCategoriesPanel";
import { api, errorText } from "../services/api";
import type { MyTeam, TeamNotice, TeamPerson } from "../types";
import { label } from "../utils/format";

const categories = [
  ["General", "General Announcement"],
  ["Project", "Project Update"],
  ["Plan", "Monthly Plan"],
  ["Deadline", "Deadline Reminder"],
  ["Meeting", "Meeting"],
  ["Blocker", "Blocker"],
] as const;

export function ManageTeamPage() {
  const { user } = useAuth();
  const [team, setTeam] = useState<MyTeam | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [addOpen, setAddOpen] = useState(false);
  const [sendOpen, setSendOpen] = useState(false);

  async function load() {
    setTeam(await api<MyTeam>("/api/my-team"));
  }

  useEffect(() => { load().catch((err) => setError(errorText(err))); }, []);

  if (error) return <p className="text-sm text-rose-700">{error}</p>;
  if (!team) return <Loading label="Loading your team" />;

  return (
    <div>
      <PageHeader
        title={team.canManage ? "Manage My Team" : "My Team"}
        subtitle={team.departmentName}
        actions={team.canManage ? (
          <>
            <Button variant="ghost" onClick={() => setAddOpen(true)}>Add team member</Button>
            <Button onClick={() => setSendOpen(true)}>Send notification</Button>
            <Link className="inline-flex items-center rounded-xl border border-line bg-white px-3 py-2 text-sm font-semibold" to="/my-team/history">History</Link>
          </>
        ) : undefined}
      />
      {message ? <p className="mb-4 text-sm text-emerald-800">{message}</p> : null}
      <div className="mb-5 grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        <Stat label="Department" value={team.departmentName} />
        <Stat label="Team lead" value={team.teamLeadName ?? "Not assigned"} />
        <Stat label="Active members" value={String(team.activeMembers)} />
        <Stat label="Pending invitations" value={String(team.pendingInvitations)} />
        <Stat label="Active projects" value={String(team.activeProjects)} />
      </div>
      <div className="overflow-x-auto rounded-2xl border border-line bg-card">
        <table className="w-full min-w-[720px] text-left text-sm">
          <thead className="border-b border-line text-xs tracking-wide text-muted uppercase">
            <tr>
              <th className="px-4 py-3">Name</th>
              <th className="px-4 py-3">Email</th>
              <th className="px-4 py-3">Job title</th>
              <th className="px-4 py-3">Account</th>
              <th className="px-4 py-3">Membership</th>
              {team.canManage ? <th className="px-4 py-3" /> : null}
            </tr>
          </thead>
          <tbody>
            {team.members.map((person) => (
              <tr key={person.id} className="border-b border-line last:border-0">
                <td className="px-4 py-3 font-semibold">{person.name}</td>
                <td className="px-4 py-3">{person.email}</td>
                <td className="px-4 py-3">{person.jobTitle ?? "—"}</td>
                <td className="px-4 py-3">{person.isActive ? <Badge value="Active" /> : "Inactive"}</td>
                <td className="px-4 py-3">{label(person.role)}</td>
                {team.canManage ? (
                  <td className="px-4 py-3 text-right">
                    {person.role === "TeamMember" && person.id !== user?.id ? (
                      <Button variant="ghost" onClick={() => remove(person)}>Remove</Button>
                    ) : null}
                  </td>
                ) : null}
              </tr>
            ))}
            {team.invitations.map((invite) => (
              <tr key={invite.id} className="border-b border-line last:border-0">
                <td className="px-4 py-3 font-semibold">{invite.name}</td>
                <td className="px-4 py-3">{invite.email}</td>
                <td className="px-4 py-3">{invite.jobTitle ?? "—"}</td>
                <td className="px-4 py-3"><Badge value="Pending" /></td>
                <td className="px-4 py-3">Invitation pending</td>
                <td className="px-4 py-3 text-right"><Button variant="ghost" onClick={() => revoke(invite.id)}>Revoke</Button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <WorkCategoriesPanel departmentId={team.departmentId} departmentName={team.departmentName} canManage={team.canManage} />
      {team.canManage ? (
        <>
          <AddMember open={addOpen} onClose={() => setAddOpen(false)} onDone={(text) => { setMessage(text); setAddOpen(false); load().catch((err) => setError(errorText(err))); }} />
          <SendNotice open={sendOpen} team={team} onClose={() => setSendOpen(false)} onDone={(text) => { setMessage(text); setSendOpen(false); }} />
        </>
      ) : null}
    </div>
  );

  async function remove(person: TeamPerson) {
    if (!window.confirm(`Remove ${person.name} from ${team?.departmentName}? Their work history stays.`)) return;
    setError("");
    try {
      await api(`/api/my-team/members/${person.id}`, { method: "DELETE" });
      setMessage(`${person.name} was removed from the team.`);
      await load();
    } catch (err) {
      setError(errorText(err));
    }
  }

  async function revoke(id: string) {
    setError("");
    try {
      await api(`/api/my-team/invitations/${id}/revoke`, { method: "POST" });
      setMessage("Invitation revoked.");
      await load();
    } catch (err) {
      setError(errorText(err));
    }
  }
}

function AddMember({ open, onClose, onDone }: { open: boolean; onClose: () => void; onDone: (message: string) => void }) {
  const [mode, setMode] = useState<"existing" | "invite">("existing");
  const [search, setSearch] = useState("");
  const [results, setResults] = useState<TeamPerson[]>([]);
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [jobTitle, setJobTitle] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function find(event: FormEvent) {
    event.preventDefault();
    setError("");
    try {
      setResults(await api<TeamPerson[]>(`/api/my-team/candidates?search=${encodeURIComponent(search)}`));
    } catch (err) {
      setError(errorText(err));
    }
  }

  async function add(person: TeamPerson) {
    setBusy(true);
    setError("");
    try {
      await api("/api/my-team/members", { method: "POST", body: JSON.stringify({ userId: person.id }) });
      onDone(`${person.name} was added to your team.`);
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  async function invite(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      await api("/api/my-team/invitations", { method: "POST", body: JSON.stringify({ name, email, jobTitle }) });
      onDone(`Invitation sent to ${email}. They can sign in after they set a password.`);
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal open={open} title="Add team member" onClose={onClose}>
      <div className="mb-4 flex gap-2">
        <Button variant={mode === "existing" ? "primary" : "ghost"} onClick={() => setMode("existing")}>Existing employee</Button>
        <Button variant={mode === "invite" ? "primary" : "ghost"} onClick={() => setMode("invite")}>Invite new member</Button>
      </div>
      {error ? <p className="mb-3 text-sm text-rose-700">{error}</p> : null}
      {mode === "existing" ? (
        <form onSubmit={find} className="space-y-3">
          <Field label="Search by name or email">
            <input className={inputClass} value={search} onChange={(event) => setSearch(event.target.value)} />
          </Field>
          <Button type="submit" variant="secondary">Search</Button>
          <div className="space-y-2">
            {results.map((person) => (
              <div key={person.id} className="flex items-center justify-between gap-3 rounded-xl border border-line px-3 py-2">
                <div>
                  <div className="font-semibold">{person.name}</div>
                  <div className="text-xs text-muted">{person.email} · {person.departmentName ?? "No department"} · {person.isActive ? "Active" : "Inactive"}</div>
                </div>
                <Button type="button" disabled={busy} onClick={() => add(person)}>Add</Button>
              </div>
            ))}
          </div>
        </form>
      ) : (
        <form onSubmit={invite} className="space-y-3">
          <Field label="Name"><input className={inputClass} value={name} onChange={(event) => setName(event.target.value)} required /></Field>
          <Field label="Work email"><input className={inputClass} type="email" value={email} onChange={(event) => setEmail(event.target.value)} required /></Field>
          <Field label="Job title"><input className={inputClass} value={jobTitle} onChange={(event) => setJobTitle(event.target.value)} /></Field>
          <Button type="submit" disabled={busy}>Send invitation</Button>
        </form>
      )}
    </Modal>
  );
}

function SendNotice({ open, team, onClose, onDone }: { open: boolean; team: MyTeam; onClose: () => void; onDone: (message: string) => void }) {
  const { user } = useAuth();
  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [category, setCategory] = useState("General");
  const [channel, setChannel] = useState("Both");
  const [everyone, setEveryone] = useState(true);
  const [copyToMe, setCopyToMe] = useState(false);
  const [linkPlan, setLinkPlan] = useState(false);
  const [projectId, setProjectId] = useState("");
  const [selected, setSelected] = useState<string[]>([]);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const active = team.members.filter((person) => person.isActive);
  const preview = useMemo(() => {
    const chosen = everyone ? active : active.filter((person) => selected.includes(person.id));
    if (copyToMe && user && !chosen.some((person) => person.id === user.id)) {
      const self = active.find((person) => person.id === user.id);
      if (self) return [...chosen, self];
    }
    return chosen;
  }, [active, everyone, selected, copyToMe, user]);

  function toggle(id: string) {
    setSelected((current) => current.includes(id) ? current.filter((item) => item !== id) : [...current, id]);
  }

  async function send(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      await api("/api/my-team/notifications", {
        method: "POST",
        body: JSON.stringify({
          title,
          body,
          category,
          channel,
          everyone,
          copyToMe,
          linkPlan: linkPlan && !projectId,
          projectId: projectId || null,
          userIds: everyone ? [] : selected,
        }),
      });
      onDone("Notification queued. In-app messages are available now. Email is handed to the mail service in the background.");
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal open={open} title="Send notification" onClose={onClose}>
      <form onSubmit={send} className="space-y-3">
        {error ? <p className="text-sm text-rose-700">{error}</p> : null}
        <Field label="Subject"><input className={inputClass} value={title} onChange={(event) => setTitle(event.target.value)} required /></Field>
        <Field label="Message"><textarea className={`${inputClass} min-h-28`} value={body} onChange={(event) => setBody(event.target.value)} required /></Field>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Category">
            <select className={inputClass} value={category} onChange={(event) => setCategory(event.target.value)}>
              {categories.map(([value, text]) => <option key={value} value={value}>{text}</option>)}
            </select>
          </Field>
          <Field label="Delivery">
            <select className={inputClass} value={channel} onChange={(event) => setChannel(event.target.value)}>
              <option value="Both">Email and in-app</option>
              <option value="Email">Email</option>
              <option value="InApp">In-app</option>
            </select>
          </Field>
        </div>
        <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={everyone} onChange={(event) => setEveryone(event.target.checked)} /> All active team members</label>
        {everyone ? null : (
          <div className="max-h-36 space-y-1 overflow-y-auto rounded-xl border border-line p-2">
            {active.map((person) => (
              <label key={person.id} className="flex items-center gap-2 text-sm">
                <input type="checkbox" checked={selected.includes(person.id)} onChange={() => toggle(person.id)} />
                {person.name}
              </label>
            ))}
          </div>
        )}
        <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={copyToMe} onChange={(event) => setCopyToMe(event.target.checked)} /> Send a copy to my email</label>
        <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={linkPlan} onChange={(event) => setLinkPlan(event.target.checked)} /> Link the department monthly plan</label>
        <Field label="Related project">
          <select className={inputClass} value={projectId} onChange={(event) => setProjectId(event.target.value)}>
            <option value="">None</option>
            {team.projects.map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}
          </select>
        </Field>
        <div className="rounded-xl bg-slate-50 px-3 py-2 text-sm">
          <div className="font-semibold">Recipients ({preview.length})</div>
          <div className="text-muted">{preview.map((person) => person.name).join(", ") || "Nobody selected"}</div>
        </div>
        <Button type="submit" disabled={busy || preview.length === 0}>Send</Button>
      </form>
    </Modal>
  );
}

export function TeamHistoryPage({ organization = false }: { organization?: boolean }) {
  const [items, setItems] = useState<TeamNotice[] | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  async function load() {
    setItems(await api<TeamNotice[]>(organization ? "/api/team-notices" : "/api/my-team/notifications"));
  }

  useEffect(() => { load().catch((err) => setError(errorText(err))); }, [organization]);

  async function retry(id: string) {
    setError("");
    try {
      await api(`/api/my-team/notifications/${id}/retry`, { method: "POST" });
      setMessage("Failed deliveries were queued again. Messages already accepted by the mail server are not sent twice.");
      await load();
    } catch (err) {
      setError(errorText(err));
    }
  }

  if (error && !items) return <p className="text-sm text-rose-700">{error}</p>;
  if (!items) return <Loading label="Loading notification history" />;

  return (
    <div>
      <PageHeader title={organization ? "Team notices" : "Notification history"} subtitle="Sent to the mail provider means the mail server accepted the message. It does not mean the inbox confirmed delivery." />
      {message ? <p className="mb-4 text-sm text-emerald-800">{message}</p> : null}
      {error ? <p className="mb-4 text-sm text-rose-700">{error}</p> : null}
      <div className="space-y-3">
        {items.length === 0 ? <p className="text-sm text-muted">No team notifications yet.</p> : null}
        {items.map((item) => (
          <article key={item.id} className="rounded-2xl border border-line bg-card p-4">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h2 className="font-semibold">{item.title}</h2>
                <p className="text-xs text-muted">{item.senderName} · {new Date(item.createdAt).toLocaleString()} · {item.recipientCount} recipients · {label(item.channel)} · {label(item.category)}</p>
              </div>
              <Badge value={item.status} />
            </div>
            <p className="mt-2 whitespace-pre-wrap text-sm">{item.body}</p>
            {item.failedRecipients.length > 0 ? <p className="mt-2 text-sm text-rose-700">Failed: {item.failedRecipients.join(", ")}</p> : null}
            {item.canRetry ? <div className="mt-3"><Button variant="ghost" onClick={() => retry(item.id)}>Retry failed</Button></div> : null}
          </article>
        ))}
      </div>
    </div>
  );
}
