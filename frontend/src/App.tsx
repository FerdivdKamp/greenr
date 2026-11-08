import { useState, useSyncExternalStore } from "react";
import type { FormEvent } from "react";
import { Link, Outlet } from "react-router-dom";
import { http } from "./lib/http";
import { sessionStore } from "./lib/session";

export default function App() {
  const { session, message } = useSyncExternalStore(sessionStore.subscribe, sessionStore.getSnapshot);
  const [identifier, setIdentifier] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function signIn(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const tokens = await http.post<{ accessToken: string; refreshToken: string }>(
        "/api/auth/login", { identifier: identifier.trim(), password }, { anonymous: true },
      );
      if (!tokens.accessToken || !tokens.refreshToken) throw new Error("The API returned an invalid sign-in response.");
      setPassword("");
      sessionStore.set(tokens);
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : "Sign-in failed.");
    } finally {
      setBusy(false);
    }
  }

  function signOut() {
    const refreshToken = session?.refreshToken;
    sessionStore.set(null, "You have signed out.");
    const signedOutState = sessionStore.getSnapshot();
    if (refreshToken) {
      void http.post<void>("/api/auth/logout", { refreshToken }, { anonymous: true })
        .catch(() => {
          if (sessionStore.getSnapshot() === signedOutState) {
            sessionStore.set(null, "Signed out locally. The server could not revoke the session.");
          }
        });
    }
  }

  return (
    <div style={{ backgroundColor: "#2c552e", color: "white", minHeight: "100vh", padding: "16px" }}>
      <header>
        <nav style={{ display: "flex", gap: "16px" }}>
          <Link style={{ color: "white" }} to="/">Home</Link>
          {session && <><Link style={{ color: "white" }} to="/items">Items</Link><Link style={{ color: "white" }} to="/questionnaires">Questionnaires</Link></>}
        </nav>
      </header>
      <main>
        {session ? <><p>Signed in. <button onClick={signOut}>Sign out</button></p><Outlet /></> : (
          <section>
            <h1>Sign in</h1>
            {message && <p role="status">{message}</p>}
            <form onSubmit={signIn}>
              <p><label>Username or email <input autoComplete="username" value={identifier} onChange={e => setIdentifier(e.target.value)} required /></label></p>
              <p><label>Password <input type="password" autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} required /></label></p>
              {error && <p role="alert">{error}</p>}
              <button disabled={busy}>{busy ? "Signing in…" : "Sign in"}</button>
            </form>
          </section>
        )}
      </main>
    </div>
  );
}
