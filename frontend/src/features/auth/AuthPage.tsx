import { useState } from "react";
import type { FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { http } from "../../lib/http";
import { sessionStore } from "../../lib/session";
import type { Session } from "../../lib/session";

export function AuthPage({ register = false }: { register?: boolean }) {
  const navigate = useNavigate();
  const [identifier, setIdentifier] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    if (register && !/^[a-zA-Z0-9_.-]{1,20}$/.test(identifier.trim())) { setError("Username must be 1–20 letters, numbers, dots, underscores or hyphens."); return; }
    if (register && (password.length < 8 || new TextEncoder().encode(password).length > 72)) { setError("Password must be at least 8 characters and at most 72 UTF-8 bytes."); return; }
    setBusy(true);
    try {
      if (register) await http.post("/api/auth/register", { username: identifier.trim(), email: email.trim(), password }, { anonymous: true });
      const tokens = await http.post<Session>("/api/auth/login", { identifier: identifier.trim(), password }, { anonymous: true });
      if (!tokens.accessToken || !tokens.refreshToken) throw new Error("The API returned an invalid sign-in response.");
      sessionStore.set(tokens);
      await navigate("/account");
    } catch (failure) { setError(failure instanceof Error ? failure.message : "Authentication failed."); }
    finally { setBusy(false); }
  }
  return <section className="auth-card">
    <h1>{register ? "Create account" : "Sign in"}</h1>
    <form onSubmit={submit}>
      <label htmlFor="identifier">{register ? "Username" : "Username or email"}</label>
      <input id="identifier" autoComplete="username" value={identifier} onChange={e => setIdentifier(e.target.value)} required maxLength={register ? 20 : 254} />
      {register && <><label htmlFor="email">Email</label><input id="email" type="email" autoComplete="email" maxLength={254} value={email} onChange={e => setEmail(e.target.value)} required /></>}
      <label htmlFor="password">Password</label>
      <input id="password" type="password" autoComplete={register ? "new-password" : "current-password"} value={password} onChange={e => setPassword(e.target.value)} required aria-describedby={register ? "password-help" : undefined} />
      {register && <small id="password-help">At least 8 characters; maximum 72 UTF-8 bytes.</small>}
      {error && <p role="alert">{error}</p>}
      <button disabled={busy}>{busy ? "Please wait…" : register ? "Create account" : "Sign in"}</button>
    </form>
    <p>{register ? <>Already have an account? <Link to="/login">Sign in</Link></> : <>New to greenr? <Link to="/register">Create an account</Link></>}</p>
  </section>;
}
