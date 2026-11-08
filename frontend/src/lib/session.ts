export type Session = { accessToken: string; refreshToken: string };
type SessionState = { session: Session | null; message: string | null };
const key = "greenr.session";
function load(): Session | null {
  try {
    const value: unknown = JSON.parse(sessionStorage.getItem(key) || "null");
    if (value && typeof value === "object" && "accessToken" in value && "refreshToken" in value && typeof value.accessToken === "string" && value.accessToken && typeof value.refreshToken === "string" && value.refreshToken) return value as Session;
  } catch { /* Storage may be unavailable or malformed. */ }
  return null;
}
let state: SessionState = { session: load(), message: null };
const listeners = new Set<() => void>();
const identityListeners = new Set<() => void>();
export const sessionStore = {
  getSnapshot: () => state,
  subscribe: (listener: () => void) => { listeners.add(listener); return () => { listeners.delete(listener); }; },
  subscribeIdentity: (listener: () => void) => { identityListeners.add(listener); return () => { identityListeners.delete(listener); }; },
  set(session: Session | null, message: string | null = null, rotated = false) {
    try { if (session) sessionStorage.setItem(key, JSON.stringify(session)); else sessionStorage.removeItem(key); } catch { /* Continue in memory. */ }
    state = { session, message };
    if (!rotated) identityListeners.forEach(listener => listener());
    listeners.forEach(listener => listener());
  },
};
