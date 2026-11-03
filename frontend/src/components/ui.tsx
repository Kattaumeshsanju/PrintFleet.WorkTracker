import { createContext, useContext, useEffect, useMemo, useState } from "react";
import type { ButtonHTMLAttributes, ReactNode } from "react";
import { label } from "../utils/format";

const tones: Record<string, string> = {
  Completed: "bg-emerald-50 text-emerald-800 ring-emerald-200",
  InProgress: "bg-sky-50 text-sky-900 ring-sky-200",
  Blocked: "bg-amber-50 text-amber-950 ring-amber-200",
  Planned: "bg-stone-100 text-stone-700 ring-stone-200",
  Cancelled: "bg-stone-100 text-stone-500 ring-stone-200",
  Submitted: "bg-emerald-50 text-emerald-800 ring-emerald-200",
  Pending: "bg-amber-50 text-amber-950 ring-amber-200",
  Open: "bg-sky-50 text-sky-900 ring-sky-200",
  Locked: "bg-stone-200 text-stone-800 ring-stone-300",
  Active: "bg-emerald-50 text-emerald-800 ring-emerald-200",
  OnHold: "bg-amber-50 text-amber-950 ring-amber-200",
  Ongoing: "bg-sky-50 text-sky-900 ring-sky-200",
  Overdue: "bg-rose-50 text-rose-900 ring-rose-200",
  Failed: "bg-rose-50 text-rose-900 ring-rose-200",
  Critical: "bg-rose-50 text-rose-900 ring-rose-200",
  High: "bg-orange-50 text-orange-950 ring-orange-200",
  Medium: "bg-sky-50 text-sky-900 ring-sky-200",
  Low: "bg-stone-100 text-stone-600 ring-stone-200",
};

export const inputClass = "w-full rounded-xl border border-line bg-white px-3 py-2 text-sm text-ink outline-none transition focus:border-brass focus:ring-2 focus:ring-brass/25 disabled:bg-slate-50 disabled:text-muted";

export function Badge({ value }: { value: string }) {
  return <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-semibold ring-1 ${tones[value] ?? "bg-stone-100 text-stone-700 ring-stone-200"}`}><span className="sr-only">Status: </span>{label(value)}</span>;
}

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & { variant?: "primary" | "secondary" | "ghost" | "danger" };

export function Button({ variant = "primary", className = "", ...props }: ButtonProps) {
  const styles = {
    primary: "bg-brass text-white hover:bg-[#a06618]",
    secondary: "bg-ink text-white hover:bg-[#101828]",
    ghost: "border border-line bg-white text-ink hover:bg-slate-50",
    danger: "bg-rose-700 text-white hover:bg-rose-800",
  }[variant];
  return <button className={`inline-flex items-center justify-center gap-2 rounded-xl px-3 py-2 text-sm font-semibold transition hover:brightness-[0.97] disabled:cursor-not-allowed disabled:opacity-50 ${styles} ${className}`} {...props} />;
}

export function Field({ label: text, hint, required, children }: { label: string; hint?: string; required?: boolean; children: ReactNode }) {
  return (
    <label className="block text-sm">
      <span className="mb-1.5 block font-semibold text-ink">{text}{required ? <span className="text-rose-700"> *</span> : null}</span>
      {children}
      {hint ? <span className="mt-1 block text-xs text-muted">{hint}</span> : null}
    </label>
  );
}

export function Modal({ open, title, children, onClose }: { open: boolean; title: string; children: ReactNode; onClose: () => void }) {
  useEffect(() => {
    if (!open) return;
    function onKey(event: KeyboardEvent) {
      if (event.key === "Escape") onClose();
    }
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [open, onClose]);
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-ink/50 p-4 sm:p-8" onMouseDown={onClose}>
      <div className="my-4 w-full max-w-xl rounded-2xl border border-line bg-card p-5 shadow-xl" role="dialog" aria-modal="true" aria-label={title} onMouseDown={(event) => event.stopPropagation()}>
        <div className="mb-4 flex items-start justify-between gap-4">
          <h2 className="font-display text-xl font-semibold">{title}</h2>
          <button className="rounded-lg px-2 py-1 text-sm text-muted hover:bg-slate-100" onClick={onClose}>Close</button>
        </div>
        {children}
      </div>
    </div>
  );
}

export function ConfirmDialog({ open, title, body, confirmLabel, busy, onConfirm, onClose }: { open: boolean; title: string; body: string; confirmLabel: string; busy?: boolean; onConfirm: () => void; onClose: () => void }) {
  return (
    <Modal open={open} title={title} onClose={onClose}>
      <p className="text-sm text-muted">{body}</p>
      <div className="mt-4 flex justify-end gap-2">
        <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
        <Button type="button" variant="danger" disabled={busy} onClick={onConfirm}>{busy ? "Working…" : confirmLabel}</Button>
      </div>
    </Modal>
  );
}

export function Stat({ label: text, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="rounded-2xl border border-line bg-card px-4 py-3 shadow-[var(--shadow-card)]">
      <div className="text-[11px] font-semibold tracking-[0.12em] text-muted uppercase">{text}</div>
      <div className="mt-1.5 font-display text-2xl font-semibold tracking-tight">{value}</div>
      {hint ? <div className="mt-1 text-xs text-muted">{hint}</div> : null}
    </div>
  );
}

export function ProgressMeter({ value, label: text }: { value: number | null; label: string }) {
  const ready = value !== null && !Number.isNaN(value);
  return (
    <div>
      <div className="mb-1 flex justify-between text-xs text-muted"><span>{text}</span><span>{ready ? `${value}%` : "No counted goals"}</span></div>
      <div className="h-1.5 overflow-hidden rounded-full bg-slate-200" role="meter" aria-label={text} aria-valuemin={0} aria-valuemax={100} aria-valuenow={ready ? value : 0}>
        <div className="h-full rounded-full bg-brass" style={{ width: `${ready ? Math.max(0, Math.min(100, value)) : 0}%` }} />
      </div>
    </div>
  );
}

export function Skeleton({ className = "h-20" }: { className?: string }) {
  return <div className={`animate-pulse rounded-xl bg-slate-200/80 ${className}`} />;
}

export function Loading({ label: text = "Loading" }: { label?: string }) {
  return (
    <div className="space-y-3" role="status" aria-live="polite">
      <span className="sr-only">{text}</span>
      <Skeleton className="h-8 w-56" />
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">{[0, 1, 2, 3].map((item) => <Skeleton key={item} />)}</div>
    </div>
  );
}

export function Alert({ children }: { children: ReactNode }) {
  return <div role="alert" className="rounded-xl border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-900">{children}</div>;
}

export function Empty({ title, body, action }: { title: string; body: string; action?: ReactNode }) {
  return (
    <div className="rounded-2xl border border-dashed border-line bg-card px-6 py-10 text-center">
      <h3 className="font-display text-lg font-semibold">{title}</h3>
      <p className="mx-auto mt-1 max-w-md text-sm text-muted">{body}</p>
      {action ? <div className="mt-4">{action}</div> : null}
    </div>
  );
}

export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: string; actions?: ReactNode }) {
  return (
    <div className="mb-5 flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 className="font-display text-2xl font-semibold tracking-tight sm:text-3xl">{title}</h1>
        {subtitle ? <p className="mt-1 text-sm text-muted">{subtitle}</p> : null}
      </div>
      {actions ? <div className="flex flex-wrap gap-2">{actions}</div> : null}
    </div>
  );
}

export function Panel({ title, children, action }: { title?: string; children: ReactNode; action?: ReactNode }) {
  return (
    <section className="rounded-2xl border border-line bg-card p-4 shadow-sm">
      {title ? (
        <div className="mb-3 flex items-center justify-between gap-3">
          <h2 className="font-display text-lg font-semibold">{title}</h2>
          {action}
        </div>
      ) : null}
      {children}
    </section>
  );
}

type ToastItem = { id: number; message: string; tone: "ok" | "error" };
const ToastContext = createContext<(message: string, tone?: "ok" | "error") => void>(() => undefined);

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const push = useMemo(() => (message: string, tone: "ok" | "error" = "ok") => {
    const id = Date.now() + Math.random();
    setToasts((current) => [...current, { id, message, tone }]);
    window.setTimeout(() => setToasts((current) => current.filter((item) => item.id !== id)), 4200);
  }, []);

  return (
    <ToastContext.Provider value={push}>
      {children}
      <div className="fixed right-4 bottom-4 z-[60] flex w-[min(100%-2rem,22rem)] flex-col gap-2">
        {toasts.map((toast) => (
          <div key={toast.id} className={`rounded-xl px-4 py-3 text-sm font-medium text-white shadow-lg ${toast.tone === "error" ? "bg-rose-800" : "bg-ink"}`}>
            {toast.message}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  return useContext(ToastContext);
}
