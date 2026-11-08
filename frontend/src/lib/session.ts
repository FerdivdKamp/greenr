type Session = { accessToken: string; refreshToken: string };
type SessionState = { session: Session | null; message: string | null };

let state: SessionState = { session: null, message: null };
const listeners = new Set<() => void>();

export const sessionStore = {
  getSnapshot: () => state,
  subscribe: (listener: () => void) => {
    listeners.add(listener);
    return () => { listeners.delete(listener); };
  },
  set(session: Session | null, message: string | null = null) {
    state = { session, message };
    listeners.forEach(listener => listener());
  },
};
