import { useEffect, useState } from "react";
import { Bell, CalendarClock, ChartColumn, ClipboardList, FolderKanban, LayoutDashboard, LogOut, Menu, Settings, Users, X } from "lucide-react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { api } from "../services/api";
import type { Notice } from "../types";
import { greeting, label, longDate, todayInput } from "../utils/format";

const itemClass = ({ isActive }: { isActive: boolean }) =>
  `flex items-center gap-3 rounded-xl px-3 py-2 text-sm font-medium ${isActive ? "bg-white/10 text-white" : "text-[#d5cec2] hover:bg-white/5"}`;

export function AppLayout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [notices, setNotices] = useState<Notice[]>([]);
  const [showNotes, setShowNotes] = useState(false);
  const lead = user?.role === "TeamLead";

  useEffect(() => {
    let alive = true;
    const load = () => api<Notice[]>("/api/notifications").then((items) => { if (alive) setNotices(items); }).catch(() => undefined);
    load();
    const timer = window.setInterval(load, 60000);
    return () => { alive = false; window.clearInterval(timer); };
  }, []);

  const links = lead
    ? [
        { to: "/", label: "Dashboard", icon: LayoutDashboard, end: true },
        { to: "/monthly-plan", label: "My Monthly Plan", icon: ClipboardList },
        { to: "/daily", label: "My Daily Timeline", icon: CalendarClock },
        { to: "/reports/me", label: "My Reports", icon: ChartColumn },
        { to: "/team", label: "Team", icon: Users },
        { to: "/plans", label: "Monthly Plans", icon: ClipboardList },
        { to: "/team-today", label: "Team Today", icon: CalendarClock },
        { to: "/projects", label: "Projects", icon: FolderKanban },
        { to: "/reports/team", label: "Team Reports", icon: ChartColumn },
        { to: "/settings", label: "Settings", icon: Settings },
      ]
    : [
        { to: "/", label: "Dashboard", icon: LayoutDashboard, end: true },
        { to: "/monthly-plan", label: "My Monthly Plan", icon: ClipboardList },
        { to: "/plans", label: "Monthly Plans", icon: ClipboardList },
        { to: "/daily", label: "My Daily Timeline", icon: CalendarClock },
        { to: "/reports/me", label: "My Reports", icon: ChartColumn },
        { to: "/settings", label: "Settings", icon: Settings },
      ];

  return (
    <div className="min-h-screen bg-paper text-ink lg:flex">
      {open ? <button className="fixed inset-0 z-30 bg-ink/40 lg:hidden" onClick={() => setOpen(false)} aria-label="Close menu" /> : null}
      <aside className={`fixed inset-y-0 left-0 z-40 flex w-72 flex-col bg-[#12171f] px-4 py-5 text-white transition-transform lg:static lg:translate-x-0 ${open ? "translate-x-0" : "-translate-x-full"}`}>
        <div className="mb-6 flex items-center gap-3 px-2">
          <div className="grid h-10 w-10 place-items-center rounded-xl bg-brass font-display text-sm font-bold">WT</div>
          <div>
            <div className="font-display text-lg font-semibold leading-none">WorkTracker</div>
            <div className="mt-1 text-xs text-[#b7b0a4]">Engineering work ledger</div>
          </div>
        </div>
        <nav className="flex flex-1 flex-col gap-1 overflow-y-auto">
          {lead ? <div className="px-3 pt-1 pb-1 text-[11px] font-semibold tracking-[0.16em] text-[#8f877b] uppercase">My Work</div> : null}
          {links.slice(0, lead ? 4 : links.length).map((link) => (
            <NavLink key={link.to} to={link.to} end={link.end} className={itemClass} onClick={() => setOpen(false)}>
              <link.icon size={18} /> {link.label}
            </NavLink>
          ))}
          {lead ? <div className="mt-4 px-3 pt-1 pb-1 text-[11px] font-semibold tracking-[0.16em] text-[#8f877b] uppercase">Team</div> : null}
          {lead ? links.slice(4).map((link) => (
            <NavLink key={link.to} to={link.to} className={itemClass} onClick={() => setOpen(false)}>
              <link.icon size={18} /> {link.label}
            </NavLink>
          )) : null}
        </nav>
        <div className="mt-4 border-t border-white/10 pt-4">
          <div className="px-2 text-sm font-semibold">{user?.name}</div>
          <div className="px-2 text-xs text-[#b7b0a4]">{user?.jobTitle ?? label(user?.role ?? "")}</div>
          <button className="mt-3 flex items-center gap-2 px-2 text-sm text-[#d5cec2]" onClick={() => { logout(); navigate("/login"); }}>
            <LogOut size={16} /> Sign out
          </button>
        </div>
      </aside>
      <div className="min-w-0 flex-1">
        <header className="sticky top-0 z-20 flex items-center justify-between gap-3 border-b border-line bg-paper/95 px-4 py-3 backdrop-blur lg:px-8">
          <button className="rounded-lg border border-line bg-card p-2 lg:hidden" onClick={() => setOpen(true)}><Menu size={18} /></button>
          <div className="hidden text-sm text-muted sm:block">{user ? greeting(user.name) : ""} · {longDate(todayInput())}</div>
          <div className="relative ml-auto">
            <button className="relative rounded-xl border border-line bg-card p-2" onClick={() => setShowNotes((value) => !value)} aria-label="Notifications">
              <Bell size={18} />
              {notices.length > 0 ? <span className="absolute -top-1 -right-1 grid h-5 min-w-5 place-items-center rounded-full bg-brass px-1 text-[11px] font-bold text-white">{notices.length}</span> : null}
            </button>
            {showNotes ? (
              <div className="absolute right-0 mt-2 w-80 rounded-2xl border border-line bg-card p-3 shadow-xl">
                <div className="mb-2 flex items-center justify-between">
                  <div className="font-semibold">Notifications</div>
                  <button onClick={() => setShowNotes(false)}><X size={16} /></button>
                </div>
                {notices.length === 0 ? <div className="py-6 text-center text-sm text-muted">Nothing needs attention.</div> : (
                  <div className="max-h-96 space-y-2 overflow-y-auto">
                    {notices.map((notice, index) => (
                      <button key={`${notice.title}-${index}`} className="block w-full rounded-xl bg-[#f7f3ea] px-3 py-2 text-left" onClick={() => { setShowNotes(false); if (notice.href) navigate(notice.href); }}>
                        <div className="text-sm font-semibold">{notice.title}</div>
                        <div className="text-xs text-muted">{notice.message}</div>
                      </button>
                    ))}
                  </div>
                )}
              </div>
            ) : null}
          </div>
        </header>
        <main className="mx-auto max-w-[1440px] px-4 py-6 lg:px-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
