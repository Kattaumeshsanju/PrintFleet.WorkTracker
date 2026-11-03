import { Suspense, useEffect, useRef, useState } from "react";
import { Bell, ChevronUp, LogOut, Menu, PanelLeftClose, PanelLeftOpen, X } from "lucide-react";
import { Link, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { api } from "../services/api";
import type { Notice } from "../types";
import { Loading } from "../components/ui";
import { greeting, label, longDate, todayInput } from "../utils/format";
import { activeNavItem, navigationFor, type NavItem } from "./navigation";

const collapsedKey = "wt_sidebar_collapsed";

export function AppLayout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const [open, setOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem(collapsedKey) === "1");
  const [menuOpen, setMenuOpen] = useState(false);
  const [notices, setNotices] = useState<Notice[]>([]);
  const [showNotes, setShowNotes] = useState(false);
  const profileRef = useRef<HTMLDivElement>(null);
  const acknowledged = useRef(new Set<string>());
  const onNoticesPage = isNoticesPath(pathname);
  const unread = notices.filter((notice) => notice.unread).length;

  function present(items: Notice[], reading: boolean) {
    return items.map((item) => (item.id && (reading || acknowledged.current.has(item.id)) ? { ...item, unread: false } : item));
  }

  async function openNotice(notice: Notice) {
    setShowNotes(false);
    if (notice.id) {
      try {
        await api(`/api/notifications/${notice.id}/read`, { method: "POST" });
        acknowledged.current.add(notice.id);
        setNotices((current) => present(current, onNoticesPage));
      } catch {
        // Keep the unread count when the server did not accept the update.
      }
    }
    if (notice.href && notice.href.startsWith("/") && !notice.href.startsWith("//")) navigate(notice.href);
  }

  async function markAll() {
    try {
      await api("/api/notifications/read-all", { method: "POST" });
      setNotices((current) => {
        for (const item of current) if (item.id) acknowledged.current.add(item.id);
        return present(current, true);
      });
    } catch {
      // Keep the unread count when the server did not accept the update.
    }
  }
  const sections = navigationFor(user);
  const visible = sections.flatMap((section) => section.items);
  const current = activeNavItem(pathname, visible);

  useEffect(() => {
    localStorage.setItem(collapsedKey, collapsed ? "1" : "0");
  }, [collapsed]);

  useEffect(() => {
    if (!user) {
      setNotices([]);
      return;
    }
    let alive = true;
    const load = () => api<Notice[]>("/api/notifications").then((items) => { if (alive) setNotices(present(items, false)); }).catch(() => undefined);
    load();
    const timer = window.setInterval(load, 60000);
    return () => { alive = false; window.clearInterval(timer); };
  }, [user?.id]);

  useEffect(() => {
    if (!user || !onNoticesPage || unread === 0) return;
    const ids = notices.flatMap((notice) => (notice.id && notice.unread ? [notice.id] : []));
    for (const id of ids) acknowledged.current.add(id);
    setNotices((current) => present(current, true));
    api("/api/notifications/read-all", { method: "POST" }).catch(() => {
      for (const id of ids) acknowledged.current.delete(id);
      setNotices((current) => current.map((item) => (item.id && ids.includes(item.id) ? { ...item, unread: true } : item)));
    });
  }, [user?.id, onNoticesPage, unread]);

  useEffect(() => {
    function close(event: MouseEvent) {
      if (!profileRef.current?.contains(event.target as Node)) setMenuOpen(false);
    }
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, []);

  function signOut() {
    logout();
    navigate("/login");
  }

  return (
    <div className="min-h-screen bg-paper text-ink lg:flex">
      {open ? <button className="fixed inset-0 z-30 bg-ink/40 lg:hidden" onClick={() => setOpen(false)} aria-label="Close menu" /> : null}
      <aside className={`fixed inset-y-0 left-0 z-40 flex w-[272px] flex-col bg-[#101820] px-3 py-4 text-white transition-[width,transform] duration-200 lg:static lg:translate-x-0 ${collapsed ? "lg:w-[76px]" : "lg:w-[272px]"} ${open ? "translate-x-0" : "-translate-x-full"}`}>
        <div className={`mb-4 flex items-center gap-2 ${collapsed ? "lg:justify-center" : "px-1"}`}>
          <div className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-brass font-display text-sm font-bold">WT</div>
          <div className={collapsed ? "min-w-0 lg:hidden" : "min-w-0"}>
            <div className="font-display text-lg font-semibold leading-none">WorkTracker</div>
            <div className="mt-1 text-xs text-[#b7b0a4]">Engineering work ledger</div>
          </div>
          <button
            className="ml-auto hidden h-9 w-9 place-items-center rounded-lg text-[#d5cec2] hover:bg-white/10 focus-visible:outline focus-visible:outline-2 focus-visible:outline-brass lg:grid"
            aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
            aria-expanded={!collapsed}
            onClick={() => setCollapsed((value) => !value)}
          >
            {collapsed ? <PanelLeftOpen size={18} /> : <PanelLeftClose size={18} />}
          </button>
        </div>
        <nav className="flex flex-1 flex-col overflow-y-auto" aria-label="Main">
          {sections.map((section) => (
            <div key={section.id} className="mb-3" role="group" aria-label={section.label}>
              <div className={`px-3 pb-1 text-[11px] font-semibold tracking-[0.16em] text-[#9a9286] uppercase ${collapsed ? "lg:sr-only" : ""}`}>{section.label}</div>
              <div className="flex flex-col gap-0.5">
                {section.items.map((item) => {
                  const active = current?.to === item.to && current.label === item.label;
                  const noticesItem = isTeamNotices(item);
                  const name = noticesItem && unread > 0 ? `${item.label}, ${unreadLabel(unread)}` : item.label;
                  return (
                    <Link
                      key={`${section.id}-${item.label}`}
                      to={item.to}
                      title={collapsed ? name : undefined}
                      aria-label={name}
                      aria-current={active ? "page" : undefined}
                      className={`group relative flex h-10 items-center gap-3 rounded-lg px-3 text-sm font-medium focus-visible:outline focus-visible:outline-2 focus-visible:outline-brass ${collapsed ? "lg:justify-center lg:px-0" : ""} ${active ? "bg-white/10 text-white" : "text-[#d5cec2] hover:bg-white/5 hover:text-white"}`}
                      onClick={() => setOpen(false)}
                    >
                      {active ? <span className="absolute top-2 bottom-2 left-0 w-[3px] rounded-full bg-brass" aria-hidden="true" /> : null}
                      <item.icon size={18} className="shrink-0" />
                      <span className={collapsed ? "lg:sr-only" : "truncate"}>{item.label}</span>
                      {noticesItem ? <UnreadMark count={unread} collapsed={collapsed} /> : null}
                      {collapsed ? <span className="pointer-events-none absolute top-1/2 left-full z-50 ml-2 hidden -translate-y-1/2 rounded-md bg-[#1c2430] px-2 py-1 text-xs whitespace-nowrap text-white shadow lg:group-hover:block lg:group-focus-visible:block">{name}</span> : null}
                    </Link>
                  );
                })}
              </div>
            </div>
          ))}
        </nav>
        <div ref={profileRef} className="relative mt-3 border-t border-white/10 pt-3">
          {menuOpen ? (
            <div className={`absolute z-50 w-60 rounded-xl border border-white/10 bg-[#1c2430] p-3 text-sm shadow-xl ${collapsed ? "bottom-0 left-full ml-2" : "right-0 bottom-full mb-2 left-0"}`} role="menu">
              <div className="font-semibold">{user?.name}</div>
              <div className="mt-1 text-xs break-all text-[#b7b0a4]">{user?.email}</div>
              <div className="mt-2 text-xs text-[#d5cec2]">{label(user?.role ?? "")}{user?.departmentName ? ` · ${user.departmentName}` : ""}</div>
              <button className="mt-3 flex h-9 w-full items-center gap-2 rounded-lg px-2 text-left text-[#d5cec2] hover:bg-white/10 focus-visible:outline focus-visible:outline-2 focus-visible:outline-brass" role="menuitem" onClick={signOut}>
                <LogOut size={16} /> Sign out
              </button>
            </div>
          ) : null}
          <button
            className={`flex h-11 w-full items-center gap-3 rounded-lg px-2 text-left hover:bg-white/5 focus-visible:outline focus-visible:outline-2 focus-visible:outline-brass ${collapsed ? "lg:justify-center lg:px-0" : ""}`}
            aria-haspopup="menu"
            aria-expanded={menuOpen}
            aria-label="Account menu"
            onClick={() => setMenuOpen((value) => !value)}
            onKeyDown={(event) => { if (event.key === "Escape") setMenuOpen(false); }}
          >
            <span className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-white/10 text-xs font-semibold">{initials(user?.name)}</span>
            <span className={collapsed ? "min-w-0 lg:hidden" : "min-w-0"}>
              <span className="block truncate text-sm font-semibold">{user?.name}</span>
              <span className="block truncate text-xs text-[#b7b0a4]">{label(user?.role ?? "")}</span>
            </span>
            <ChevronUp size={16} className={`ml-auto text-[#b7b0a4] ${collapsed ? "lg:hidden" : ""} ${menuOpen ? "" : "rotate-180"}`} />
          </button>
        </div>
      </aside>
        <div className="min-w-0 flex-1 overflow-x-clip">
        <header className="sticky top-0 z-20 flex items-center justify-between gap-3 border-b border-line bg-paper/95 px-4 py-3 backdrop-blur lg:px-8">
          <button className="rounded-lg border border-line bg-card p-2 lg:hidden" onClick={() => setOpen(true)} aria-label="Open menu"><Menu size={18} /></button>
          <div className="hidden text-sm text-muted sm:block">{user ? greeting(user.name) : ""} · {longDate(todayInput())}</div>
          <div className="relative ml-auto">
            <button className="relative rounded-xl border border-line bg-card p-2" onClick={() => setShowNotes((value) => !value)} aria-label={unreadLabel(unread)} aria-expanded={showNotes}>
              <Bell size={18} />
              {unread > 0 ? <span className="absolute -top-1 -right-1 grid h-5 min-w-5 place-items-center rounded-full bg-brass px-1 text-[11px] font-bold text-white">{cap(unread)}</span> : null}
            </button>
            {showNotes ? (
              <div className="absolute right-0 mt-2 w-80 rounded-2xl border border-line bg-card p-3 shadow-xl">
                <div className="mb-2 flex items-center justify-between gap-2">
                  <div className="font-semibold">Notifications</div>
                  <div className="flex items-center gap-2">
                    {unread > 0 ? <button className="text-xs font-semibold text-brass" onClick={() => markAll()}>Mark all read</button> : null}
                    <button onClick={() => setShowNotes(false)} aria-label="Close notifications"><X size={16} /></button>
                  </div>
                </div>
                {notices.length === 0 ? <div className="py-6 text-center text-sm text-muted">Nothing needs attention.</div> : (
                  <div className="max-h-96 space-y-2 overflow-y-auto">
                    {notices.map((notice, index) => (
                      <button key={notice.id ?? `${notice.title}-${index}`} className="block w-full rounded-xl bg-slate-50 px-3 py-2 text-left" onClick={() => openNotice(notice)}>
                        <div className="text-sm font-semibold">{notice.title}</div>
                        {notice.senderName ? <div className="text-xs text-muted">From {notice.senderName}</div> : null}
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
          <Suspense fallback={<Loading label="Loading the page" />}>
            <Outlet />
          </Suspense>
        </main>
      </div>
    </div>
  );
}

function isNoticesPath(pathname: string) {
  return pathname === "/my-team/history" || pathname === "/team-notices";
}

function isTeamNotices(item: NavItem) {
  return isNoticesPath(item.to);
}

function cap(count: number) {
  return count > 99 ? "99+" : String(count);
}

function unreadLabel(count: number) {
  if (count <= 0) return "Notifications";
  return count === 1 ? "1 unread notification" : `${cap(count)} unread notifications`;
}

function UnreadMark({ count, collapsed }: { count: number; collapsed: boolean }) {
  if (count <= 0) return null;
  return (
    <>
      <span className={`ml-auto grid h-5 min-w-5 place-items-center rounded-full bg-brass px-1 text-[10px] font-bold text-white ${collapsed ? "lg:hidden" : ""}`}>{cap(count)}</span>
      <span className={`absolute top-2 right-2 hidden h-2 w-2 rounded-full bg-brass ring-2 ring-[#101820] ${collapsed ? "lg:block" : ""}`} aria-hidden="true" />
    </>
  );
}

function initials(name?: string) {
  const parts = (name ?? "").trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "•";
  return parts.slice(0, 2).map((part) => part[0]?.toUpperCase() ?? "").join("");
}
