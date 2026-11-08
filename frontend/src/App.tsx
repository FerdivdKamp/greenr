import { useSyncExternalStore } from "react";
import { Link, Navigate, Outlet, useLocation } from "react-router-dom";
import { http } from "./lib/http";
import { sessionStore } from "./lib/session";

export function ProtectedRoute() {
  const { session } = useSyncExternalStore(sessionStore.subscribe, sessionStore.getSnapshot);
  const location = useLocation();
  return session ? <Outlet /> : <Navigate to="/login" replace state={{ from: location.pathname }} />;
}
export default function App() {
  const { session, message } = useSyncExternalStore(sessionStore.subscribe, sessionStore.getSnapshot);
  function signOut() {
    const refreshToken = session?.refreshToken;
    sessionStore.set(null, "You have signed out.");
    const signedOutState = sessionStore.getSnapshot();
    if (refreshToken) void http.post<void>("/api/auth/logout", { refreshToken }, { anonymous: true }).catch(() => {
      if (sessionStore.getSnapshot() === signedOutState) sessionStore.set(null, "Signed out locally. The server could not revoke the session.");
    });
  }
  return <div className="app-shell"><header><nav aria-label="Main navigation">
    <Link to="/">greenr</Link>
    {session ? <><Link to="/items">Items</Link><Link to="/questionnaires">Questionnaires</Link><Link to="/account">Account</Link><button onClick={signOut}>Sign out</button></> : <><Link to="/login">Sign in</Link><Link to="/register">Create account</Link></>}
  </nav></header><main>{message && <p role="status">{message}</p>}<Outlet /></main></div>;
}
