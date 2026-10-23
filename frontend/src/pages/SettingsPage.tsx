import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useAuth } from "../auth/AuthContext";
import { Button, Field, Loading, PageHeader, Panel, inputClass, useToast } from "../components/ui";
import { api, errorText } from "../services/api";
import type { AuditLog, Paged } from "../types";
import { canManage, label } from "../utils/format";

export function SettingsPage() {
  const { user } = useAuth();
  const toast = useToast();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [audit, setAudit] = useState<Paged<AuditLog> | null>(null);
  const lead = canManage(user?.role);

  useEffect(() => {
    if (lead) api<Paged<AuditLog>>("/api/audit?page=1&pageSize=25").then(setAudit).catch(() => undefined);
  }, [lead]);

  async function changePassword(event: FormEvent) {
    event.preventDefault();
    try {
      await api("/api/auth/change-password", { method: "POST", body: JSON.stringify({ currentPassword, newPassword }) });
      setCurrentPassword("");
      setNewPassword("");
      toast("Password updated.");
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  if (!user) return <Loading />;

  return (
    <div className="space-y-4">
      <PageHeader title="Settings" subtitle="Your account and the change history used for monthly reviews." />
      <Panel title="Profile">
        <div className="grid gap-2 text-sm sm:grid-cols-2">
          <div><span className="text-muted">Name</span><div className="font-semibold">{user.name}</div></div>
          <div><span className="text-muted">Email</span><div>{user.email}</div></div>
          <div><span className="text-muted">Role</span><div>{label(user.role)}</div></div>
          <div><span className="text-muted">Title</span><div>{user.jobTitle ?? "—"}</div></div>
        </div>
      </Panel>
      <Panel title="Change password">
        <form className="grid max-w-md gap-3" onSubmit={changePassword}>
          <Field label="Current password"><input className={inputClass} type="password" value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} required /></Field>
          <Field label="New password"><input className={inputClass} type="password" minLength={8} value={newPassword} onChange={(event) => setNewPassword(event.target.value)} required /></Field>
          <Button type="submit">Update password</Button>
        </form>
      </Panel>
      {lead ? (
        <Panel title="Audit trail">
          {!audit ? <Loading /> : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[680px] text-left text-sm">
                <thead className="text-xs text-muted uppercase"><tr><th className="py-2">When</th><th>Who</th><th>Action</th><th>Record</th><th>Details</th></tr></thead>
                <tbody>
                  {audit.items.map((item) => (
                    <tr key={item.id} className="border-t border-line">
                      <td className="py-2 whitespace-nowrap">{new Date(item.createdAt).toLocaleString()}</td>
                      <td>{item.userName ?? "System"}</td>
                      <td>{item.action}</td>
                      <td>{item.entityType}</td>
                      <td className="text-muted">{item.details}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Panel>
      ) : null}
    </div>
  );
}
