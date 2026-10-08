import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Badge, Button, Loading, PageHeader, useToast } from "../components/ui";
import { api, errorText } from "../services/api";
import type { Paged, Project, TeamDashboard, User } from "../types";
import type { DailyEntry } from "../types";
import { formatMinutes, todayInput } from "../utils/format";

export function TeamTodayPage() {
  const toast = useToast();
  const [date, setDate] = useState(todayInput());
  const [userId, setUserId] = useState("");
  const [projectId, setProjectId] = useState("");
  const [status, setStatus] = useState("");
  const [search, setSearch] = useState("");
  const [people, setPeople] = useState<User[]>([]);
  const [projects, setProjects] = useState<Project[]>([]);
  const [team, setTeam] = useState<TeamDashboard | null>(null);
  const [entries, setEntries] = useState<DailyEntry[]>([]);
  const [error, setError] = useState("");

  useEffect(() => {
    api<User[]>("/api/users").then(setPeople).catch(() => undefined);
    api<Project[]>("/api/projects").then(setProjects).catch(() => undefined);
  }, []);

  useEffect(() => {
    const query = new URLSearchParams({ workDate: date, pageSize: "100" });
    if (userId) query.set("userId", userId);
    if (projectId) query.set("projectId", projectId);
    if (status) query.set("status", status);
    if (search.trim()) query.set("search", search.trim());
    Promise.all([
      api<TeamDashboard>(`/api/dashboard/team?date=${date}`),
      api<Paged<DailyEntry>>(`/api/daily-work?${query}`),
    ]).then(([dashboard, page]) => {
      setTeam(dashboard);
      setEntries(page.items);
      setError("");
    }).catch((err) => setError(errorText(err)));
  }, [date, userId, projectId, status, search]);

  async function reopen(id: string) {
    try {
      await api("/api/daily-work/reopen", { method: "POST", body: JSON.stringify({ workDate: date, userId: id }) });
      toast("Daily update reopened.");
      setDate((value) => value);
      const dashboard = await api<TeamDashboard>(`/api/dashboard/team?date=${date}`);
      setTeam(dashboard);
    } catch (err) {
      toast(errorText(err), "error");
    }
  }

  if (error) return <div className="rounded-2xl bg-rose-50 px-4 py-3 text-rose-800">{error}</div>;
  if (!team) return <Loading />;

  const grouped = team.members
    .map((member) => ({ member, entries: entries.filter((entry) => entry.userId === member.userId) }))
    .filter((group) => group.entries.length > 0 || (!userId && !projectId && !status && !search.trim()));

  return (
    <div>
      <PageHeader title="What Did The Team Do Today?" subtitle="Every timed activity, in one pass." />
      <div className="mb-4 grid gap-2 md:grid-cols-5">
        <input className="rounded-xl border border-line bg-white px-3 py-2" type="date" value={date} onChange={(event) => setDate(event.target.value)} />
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={userId} onChange={(event) => setUserId(event.target.value)}>
          <option value="">All employees</option>
          {people.map((person) => <option key={person.id} value={person.id}>{person.name}</option>)}
        </select>
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={projectId} onChange={(event) => setProjectId(event.target.value)}>
          <option value="">All projects</option>
          {projects.map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}
        </select>
        <select className="rounded-xl border border-line bg-white px-3 py-2" value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">All statuses</option>
          <option value="Completed">Completed</option>
          <option value="InProgress">In Progress</option>
          <option value="Blocked">Blocked</option>
        </select>
        <input className="rounded-xl border border-line bg-white px-3 py-2" placeholder="Search task, work, person" value={search} onChange={(event) => setSearch(event.target.value)} />
      </div>
      <div className="mb-4 text-sm text-muted">{team.updatedToday} of {team.memberCount} submitted · {formatMinutes(team.totalMinutesToday)} logged</div>
      <div className="space-y-4">
        {grouped.map(({ member, entries: rows }) => (
          <section key={member.userId} className="rounded-2xl border border-line bg-card p-4">
            <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
              <div>
                <Link className="font-display text-xl font-semibold" to={`/team/${member.userId}`}>{member.name}</Link>
                <div className="text-xs text-muted">{member.jobTitle} · {formatMinutes(member.totalMinutes)}</div>
              </div>
              <div className="flex items-center gap-2">
                <Badge value={member.updateStatus} />
                {member.updateStatus === "Submitted" ? <Button variant="ghost" onClick={() => reopen(member.userId)}>Reopen</Button> : null}
              </div>
            </div>
            {rows.length === 0 ? <div className="text-sm text-amber-800">Daily update pending.</div> : rows.map((entry) => (
              <div key={entry.id} className="grid grid-cols-[8rem_1fr_auto] gap-3 border-t border-line py-2 text-sm">
                <div className="font-mono">{entry.startTime}–{entry.endTime}</div>
                <div><span className="font-semibold">{entry.monthlyTaskTitle}</span> · {entry.description}</div>
                <Badge value={entry.status} />
              </div>
            ))}
          </section>
        ))}
      </div>
    </div>
  );
}
