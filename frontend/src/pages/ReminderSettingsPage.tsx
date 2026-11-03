import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Badge, Button, Field, Loading, PageHeader, Panel, inputClass, useToast } from "../components/ui";
import { api, errorText } from "../services/api";

type Settings = {
  outlookReady: boolean;
  outlookStatus: string;
  sender: string;
  enabled: boolean;
  missingPlanEnabled: boolean;
  departmentSummaryEnabled: boolean;
  hour: number;
  minute: number;
  weekdays: number[];
  timeZone: string;
  recipientCount: number;
  lastSuccessAt: string | null;
  lastFailureAt: string | null;
  lastFailureDetail: string | null;
};

type Preview = { subject: string; text: string; html: string; sample: boolean };
type Delivery = {
  id: string;
  scheduledDate: string;
  departmentName: string;
  recipientEmail: string;
  subject: string;
  messageType: string;
  attempts: number;
  status: string;
  statusLabel: string;
  lastAttemptAt: string | null;
  failureDetail: string | null;
};
type Page = { items: Delivery[]; total: number };

const days = [
  [1, "Mon"],
  [2, "Tue"],
  [3, "Wed"],
  [4, "Thu"],
  [5, "Fri"],
  [6, "Sat"],
  [0, "Sun"],
] as const;

export function ReminderSettingsPage() {
  const toast = useToast();
  const [settings, setSettings] = useState<Settings | null>(null);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [history, setHistory] = useState<Page | null>(null);
  const [busy, setBusy] = useState(false);

  async function load() {
    const [nextSettings, nextHistory] = await Promise.all([
      api<Settings>("/api/plan-reminders/settings"),
      api<Page>("/api/plan-reminders/history?page=1&pageSize=25"),
    ]);
    setSettings(nextSettings);
    setHistory(nextHistory);
  }

  useEffect(() => { load().catch(() => undefined); }, []);

  function toggleDay(day: number) {
    if (!settings) return;
    const weekdays = settings.weekdays.includes(day) ? settings.weekdays.filter((item) => item !== day) : [...settings.weekdays, day];
    setSettings({ ...settings, weekdays });
  }

  async function save(event: FormEvent) {
    event.preventDefault();
    if (!settings) return;
    setBusy(true);
    try {
      setSettings(await api<Settings>("/api/plan-reminders/settings", { method: "PUT", body: JSON.stringify(settings) }));
      toast("Reminder settings saved.");
    } catch (err) {
      toast(errorText(err), "error");
    } finally {
      setBusy(false);
    }
  }

  async function showPreview() {
    try {
      setPreview(await api<Preview>("/api/plan-reminders/preview"));
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  async function sendTest() {
    if (!window.confirm("Send one test email to your own admin address? Nobody else will receive it.")) return;
    setBusy(true);
    try {
      await api("/api/plan-reminders/test", { method: "POST" });
      toast("Microsoft Graph accepted the test email. That is not delivery confirmation.");
      await load();
    } catch (err) {
      toast(errorText(err), "error");
    } finally {
      setBusy(false);
    }
  }

  if (!settings) return <Loading />;
  const time = `${String(settings.hour).padStart(2, "0")}:${String(settings.minute).padStart(2, "0")}`;

  return (
    <div className="space-y-4">
      <PageHeader title="Email reminders" subtitle="Weekday monthly-plan reminders from the authorized Outlook mailbox. The existing 5:00 PM daily reminder is unchanged." />
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Panel><div className="text-xs text-muted uppercase">Outlook</div><div className="mt-1"><Badge value={settings.outlookReady ? "Active" : "Pending"} /></div><div className="mt-2 text-sm text-muted">{settings.outlookStatus}</div></Panel>
        <Panel><div className="text-xs text-muted uppercase">Sender</div><div className="mt-1 font-semibold">{settings.sender || "Not configured"}</div></Panel>
        <Panel><div className="text-xs text-muted uppercase">Recipients this run</div><div className="font-display text-2xl font-semibold">{settings.recipientCount}</div></Panel>
        <Panel><div className="text-xs text-muted uppercase">Schedule</div><div className="mt-1 font-semibold">{time} {settings.timeZone}</div></Panel>
      </div>
      <Panel title="Reminder settings">
        <form className="grid max-w-3xl gap-4" onSubmit={save}>
          <label className="flex items-center gap-2 text-sm font-semibold"><input type="checkbox" checked={settings.enabled} onChange={(event) => setSettings({ ...settings, enabled: event.target.checked })} /> Send automated plan reminders</label>
          <label className="flex items-center gap-2 text-sm font-semibold"><input type="checkbox" checked={settings.missingPlanEnabled} onChange={(event) => setSettings({ ...settings, missingPlanEnabled: event.target.checked })} /> Remind people who have no monthly plan</label>
          <label className="flex items-center gap-2 text-sm font-semibold"><input type="checkbox" checked={settings.departmentSummaryEnabled} onChange={(event) => setSettings({ ...settings, departmentSummaryEnabled: event.target.checked })} /> Also email each team lead a department summary</label>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Time">
              <input className={inputClass} type="time" value={time} onChange={(event) => {
                const [hour, minute] = event.target.value.split(":").map(Number);
                setSettings({ ...settings, hour, minute });
              }} />
            </Field>
            <Field label="Timezone"><input className={inputClass} value={settings.timeZone} disabled /></Field>
          </div>
          <div className="flex flex-wrap gap-2">
            {days.map(([day, name]) => (
              <label key={name} className="flex items-center gap-2 rounded-full border border-line px-3 py-1 text-sm font-semibold">
                <input type="checkbox" checked={settings.weekdays.includes(day)} onChange={() => toggleDay(day)} /> {name}
              </label>
            ))}
          </div>
          <div className="flex flex-wrap gap-2">
            <Button type="submit" disabled={busy}>Save</Button>
            <Button type="button" variant="ghost" onClick={showPreview}>Preview template</Button>
            <Button type="button" variant="secondary" disabled={busy} onClick={sendTest}>Send test to me</Button>
          </div>
          <p className="text-sm text-muted">Accepted by Microsoft Graph means the request was queued. It does not mean the recipient's mailbox has received it. Credentials stay on the server.</p>
        </form>
      </Panel>
      <div className="grid gap-3 sm:grid-cols-2">
        <Panel><div className="text-xs text-muted uppercase">Last successful run</div><div className="mt-1">{settings.lastSuccessAt ? new Date(settings.lastSuccessAt).toLocaleString() : "None yet"}</div></Panel>
        <Panel><div className="text-xs text-muted uppercase">Last run with failures</div><div className="mt-1">{settings.lastFailureAt ? new Date(settings.lastFailureAt).toLocaleString() : "None"}</div><div className="text-sm text-muted">{settings.lastFailureDetail}</div></Panel>
      </div>
      {preview ? (
        <Panel title={preview.sample ? "Preview sample — not a real plan" : `Preview · ${preview.subject}`}>
          <iframe className="h-[480px] w-full rounded-xl border border-line bg-white" sandbox="" title="Email preview" srcDoc={preview.html} />
        </Panel>
      ) : null}
      <Panel title="Delivery history">
        {!history ? <Loading /> : history.items.length === 0 ? <div className="text-sm text-muted">No reminder attempts yet.</div> : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[860px] text-left text-sm">
              <thead className="text-xs text-muted uppercase"><tr><th className="py-2">When</th><th>Type</th><th>Department</th><th>Recipient</th><th>Status</th><th>Attempts</th><th>Detail</th></tr></thead>
              <tbody>
                {history.items.map((item) => (
                  <tr key={item.id} className="border-t border-line">
                    <td className="py-2 whitespace-nowrap">{item.lastAttemptAt ? new Date(item.lastAttemptAt).toLocaleString() : item.scheduledDate}</td>
                    <td>{item.messageType}</td>
                    <td>{item.departmentName}</td>
                    <td>{item.recipientEmail}</td>
                    <td>{item.statusLabel}</td>
                    <td>{item.attempts}</td>
                    <td className="text-muted">{item.failureDetail}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>
    </div>
  );
}
