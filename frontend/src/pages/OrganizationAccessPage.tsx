import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Button, ConfirmDialog, Field, PageHeader, inputClass } from "../components/ui";
import { api, errorText } from "../services/api";
import type { OrganizationAccess, OrganizationPerson } from "../types";
import { label } from "../utils/format";

const empty = { name: "", email: "", jobTitle: "", isActive: true };

export function OrganizationAccessPage() {
  const [page, setPage] = useState<OrganizationAccess | null>(null);
  const [form, setForm] = useState(empty);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [confirm, setConfirm] = useState<{ title: string; body: string; label: string; run: () => Promise<void> } | null>(null);

  async function load() {
    setPage(await api<OrganizationAccess>("/api/organization-access"));
  }

  useEffect(() => {
    load().catch((err) => setError(errorText(err)));
  }, []);

  async function invite(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    setMessage("");
    try {
      await api("/api/organization-access/ceo", { method: "POST", body: JSON.stringify(form) });
      setForm(empty);
      setMessage("Activation email sent. The CEO chooses their own password.");
      await load();
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  function revoke(id: string, name: string) {
    setConfirm({
      title: "Revoke invitation",
      body: `Revoke the CEO invitation for ${name}? The activation link will stop working.`,
      label: "Revoke",
      run: async () => {
        await api(`/api/organization-access/invitations/${id}/revoke`, { method: "POST" });
        await load();
      },
    });
  }

  async function grant(person: OrganizationPerson, kind: "overview" | "reports", granted: boolean) {
    setError("");
    try {
      const updated = await api<OrganizationPerson>(`/api/organization-access/users/${person.id}/${kind}`, {
        method: "PUT",
        body: JSON.stringify({ granted }),
      });
      setPage((current) => current ? { ...current, people: current.people.map((item) => item.id === updated.id ? updated : item) } : current);
    } catch (err) {
      setError(errorText(err));
    }
  }

  function status(person: OrganizationPerson, isActive: boolean) {
    setConfirm({
      title: isActive ? "Activate CEO account" : "Deactivate CEO account",
      body: isActive ? `${person.name} will be able to sign in with read-only organization access.` : `${person.name} will be signed out of organization pages on the next request.`,
      label: isActive ? "Activate" : "Deactivate",
      run: async () => {
        const updated = await api<OrganizationPerson>(`/api/organization-access/users/${person.id}/status`, {
          method: "POST",
          body: JSON.stringify({ isActive }),
        });
        setPage((current) => current ? { ...current, people: current.people.map((item) => item.id === updated.id ? updated : item) } : current);
      },
    });
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Organization access" subtitle="Create CEO accounts and grant overview or report access without changing a person's role. CEO access is read-only. It does not include admin rights, private daily timelines, or research records unless the person is already a CEO." />
      {error ? <p className="rounded-xl bg-rose-50 px-3 py-2 text-sm text-rose-800">{error}</p> : null}
      {message ? <p className="rounded-xl bg-emerald-50 px-3 py-2 text-sm text-emerald-800">{message}</p> : null}

      <form onSubmit={invite} className="grid gap-3 rounded-2xl border border-line bg-card p-4 md:grid-cols-2">
        <h2 className="font-display text-lg font-semibold md:col-span-2">Create a CEO account</h2>
        <Field label="Full name"><input className={inputClass} value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required /></Field>
        <Field label="Work email"><input className={inputClass} type="email" value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} required /></Field>
        <Field label="Job title"><input className={inputClass} value={form.jobTitle} onChange={(event) => setForm({ ...form, jobTitle: event.target.value })} /></Field>
        <Field label="Account status">
          <select className={inputClass} value={form.isActive ? "active" : "inactive"} onChange={(event) => setForm({ ...form, isActive: event.target.value === "active" })}>
            <option value="active">Active after the password is set</option>
            <option value="inactive">Inactive until an admin activates it</option>
          </select>
        </Field>
        <div className="md:col-span-2"><Button type="submit" disabled={busy}>{busy ? "Sending…" : "Send activation email"}</Button></div>
      </form>

      <section className="rounded-2xl border border-line bg-card p-4">
        <h2 className="font-display text-lg font-semibold">Pending CEO invitations</h2>
        {!page ? <p className="mt-2 text-sm text-muted">Loading…</p> : page.invitations.length === 0 ? <p className="mt-2 text-sm text-muted">No invitations are waiting.</p> : (
          <ul className="mt-3 divide-y divide-line text-sm">
            {page.invitations.map((invitation) => (
              <li key={invitation.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
                <span><span className="font-semibold">{invitation.name}</span> · {invitation.email}{invitation.activateOnAccept ? "" : " · inactive after setup"}</span>
                <Button type="button" variant="ghost" onClick={() => revoke(invitation.id, invitation.name)}>Revoke</Button>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="overflow-x-auto rounded-2xl border border-line bg-card">
        <table className="w-full min-w-[860px] text-left text-sm">
          <thead className="bg-slate-50 text-xs tracking-wide text-muted uppercase">
            <tr>{["Name", "Department", "Role", "Status", "Overview", "Reports", ""].map((heading) => <th key={heading} className="px-3 py-2">{heading}</th>)}</tr>
          </thead>
          <tbody>
            {page?.people.map((person) => (
              <tr key={person.id} className="border-t border-line">
                <td className="px-3 py-2"><div className="font-semibold">{person.name}</div><div className="text-muted">{person.email}</div></td>
                <td className="px-3 py-2">{person.departmentName ?? "—"}</td>
                <td className="px-3 py-2">{label(person.role)}</td>
                <td className="px-3 py-2">{person.isActive ? "Active" : "Inactive"}</td>
                <td className="px-3 py-2"><input type="checkbox" checked={person.overview} disabled={!person.grantsEditable} aria-label={`Organization overview for ${person.name}`} onChange={(event) => grant(person, "overview", event.target.checked)} /></td>
                <td className="px-3 py-2"><input type="checkbox" checked={person.reports} disabled={!person.grantsEditable} aria-label={`Organization reports for ${person.name}`} onChange={(event) => grant(person, "reports", event.target.checked)} /></td>
                <td className="px-3 py-2">{person.statusEditable ? <Button type="button" variant="ghost" onClick={() => status(person, !person.isActive)}>{person.isActive ? "Deactivate" : "Activate"}</Button> : null}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
      <ConfirmDialog
        open={confirm !== null}
        title={confirm?.title ?? ""}
        body={confirm?.body ?? ""}
        confirmLabel={confirm?.label ?? "Confirm"}
        onClose={() => setConfirm(null)}
        onConfirm={() => {
          const action = confirm?.run;
          setConfirm(null);
          if (!action) return;
          setError("");
          action().catch((err) => setError(errorText(err)));
        }}
      />
    </div>
  );
}
