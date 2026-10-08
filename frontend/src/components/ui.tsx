import { createContext, useContext, useMemo, useState } from "react";
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
  Critical: "bg-rose-50 text-rose-900 ring-rose-200",
  High: "bg-orange-50 text-orange-950 ring-orange-200",
  Medium: "bg-sky-50 text-sky-900 ring-sky-200",
  Low: "bg-stone-100 text-stone-600 ring-stone-200",
};

export const inputClass = "w-full rounded-xl border border-line bg-white px-3 py-2 text-sm outline-none ring-brass/30 focus:border-brass focus:ring-2";

export function Badge({ value }: { value: string }) {
  return <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold ring-1 ${tones[value] ?? "bg-stone-100 text-stone-700 ring-stone-200"}`}>{label(value)}</span>;
}

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & { variant?: "primary" | "secondary" | "ghost" | "danger" };

export function Button({ variant = "primary", className = "", ...props }: ButtonProps) {
  const styles = {
    primary: "bg-brass text-white hover:bg-[#a06618]",
    secondary: "bg-ink text-white hover:bg-[#101828]",
    ghost: "border border-line bg-white text-ink hover:bg-[#f7f3ea]",
    danger: "bg-rose-700 text-white hover:bg-rose-800",
  }[variant];
  return <button className={`inline-flex items-center justify-center gap-2 rounded-xl px-3 py-2 text-sm font-semibold disabled:cursor-not-allowed disabled:opacity-50 ${styles} ${className}`} {...props} />;
}

export function Field({ label: text, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block text-sm">
      <span className="mb-1.5 block font-semibold text-ink">{text}</span>
      {children}
    </label>
  );
}

export function Modal({ open, title, children, onClose }: { open: boolean; title: string; children: ReactNode; onClose: () => void }) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-ink/55 p-4 sm:p-8" onMouseDown={onClose}>
      <div className="my-4 w-full max-w-xl rounded-2xl border border-line bg-card p-5 shadow-2xl" onMouseDown={(event) => event.stopPropagation()}>
        <div className="mb-4 flex items-start justify-between gap-4">
          <h2 className="font-display text-xl font-semibold">{title}</h2>
          <button className="text-sm text-muted" onClick={onClose}>Close</button>
        </div>
        {children}
      </div>
    </div>
  );
}

export function Stat({ label: text, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="rounded-2xl border border-line bg-card px-4 py-3 shadow-sm">
      <div className="text-xs font-semibold tracking-[0.14em] text-muted uppercase">{text}</div>
      <div className="mt-2 font-display text-2xl font-semibold tracking-tight">{value}</div>
      {hint ? <div className="mt-1 text-xs text-muted">{hint}</div> : null}
    </div>
  );
}

export function Loading({ label: text = "Loading" }: { label?: string }) {
  return <div className="py-16 text-center text-sm text-muted">{text}…</div>;
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
        <h1 className="font-display text-3xl font-semibold tracking-tight">{title}</h1>
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
