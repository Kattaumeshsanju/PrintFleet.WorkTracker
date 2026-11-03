import { useState } from "react";
import type { FormEvent } from "react";
import { Navigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { Button, inputClass } from "../components/ui";
import { errorText } from "../services/api";

export function LoginPage() {
  const { user, login } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  if (user) return <Navigate to={user.role === "CEO" ? "/executive" : "/"} replace />;

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      await login(email, password);
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="grid min-h-screen lg:grid-cols-[1.1fr_0.9fr]">
      <section className="hidden flex-col justify-between bg-[#101820] p-12 text-white lg:flex">
        <div className="flex items-center gap-3">
          <div className="grid h-11 w-11 place-items-center rounded-xl bg-brass font-display font-bold">WT</div>
          <div className="font-display text-xl font-semibold">WorkTracker</div>
        </div>
        <div>
          <p className="font-display text-4xl leading-tight font-semibold">Plan the month.<br />Record the day.<br />Review the work.</p>
          <p className="mt-4 max-w-md text-[#d5cec2]">A ledger for an engineering team: monthly plans, timed daily work, and a clear view for the team lead.</p>
        </div>
        <div className="text-sm text-[#b7b0a4]">Internal engineering workspace</div>
      </section>
      <section className="flex items-center justify-center bg-paper px-6 py-12">
        <form className="w-full max-w-md" onSubmit={submit}>
          <div className="mb-6 flex items-center gap-3 lg:hidden">
            <div className="grid h-10 w-10 place-items-center rounded-xl bg-brass font-display text-sm font-bold text-white">WT</div>
            <div className="font-display text-lg font-semibold">WorkTracker</div>
          </div>
          <h1 className="font-display text-3xl font-semibold">Sign in</h1>
          <p className="mt-1 text-sm text-muted">Use your work email and password.</p>
          <label className="mt-6 block text-sm font-semibold">Email
            <input className={`${inputClass} mt-1`} type="email" autoComplete="username" value={email} onChange={(event) => setEmail(event.target.value)} required />
          </label>
          <label className="mt-3 block text-sm font-semibold">Password
            <input className={`${inputClass} mt-1`} type="password" autoComplete="current-password" value={password} onChange={(event) => setPassword(event.target.value)} required />
          </label>
          {error ? <div role="alert" className="mt-3 rounded-xl border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-900">{error}</div> : null}
          <Button className="mt-5 w-full" disabled={busy}>{busy ? "Signing in…" : "Sign in"}</Button>
        </form>
      </section>
    </div>
  );
}
