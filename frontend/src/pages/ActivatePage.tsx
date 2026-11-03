import { useState } from "react";
import type { FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Button, Field, inputClass } from "../components/ui";
import { api, errorText } from "../services/api";

export function ActivatePage() {
  const [params] = useSearchParams();
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const token = params.get("token") ?? "";

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const result = await api<{ message: string }>("/api/ceo-invitations/accept", {
        method: "POST",
        body: JSON.stringify({ token, password }),
      });
      setMessage(result.message);
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="grid min-h-screen place-items-center bg-paper px-4">
      <form onSubmit={submit} className="w-full max-w-md rounded-2xl border border-line bg-card p-6 shadow-sm">
        <h1 className="font-display text-2xl font-semibold">Activate your CEO account</h1>
        <p className="mt-1 text-sm text-muted">Choose a password. This account is read-only and does not include admin access. If an admin left the account inactive, you can sign in only after they activate it.</p>
        {message ? <p className="mt-4 text-sm text-emerald-800">{message} <Link className="font-semibold underline" to="/login">Sign in</Link></p> : (
          <div className="mt-4 space-y-3">
            {error ? <p className="text-sm text-rose-700">{error}</p> : null}
            {!token ? <p className="text-sm text-rose-700">This activation link is missing its token.</p> : null}
            <Field label="Password">
              <input className={inputClass} type="password" value={password} onChange={(event) => setPassword(event.target.value)} minLength={8} required />
            </Field>
            <Button type="submit" disabled={busy || !token}>{busy ? "Saving…" : "Set password"}</Button>
          </div>
        )}
      </form>
    </div>
  );
}
