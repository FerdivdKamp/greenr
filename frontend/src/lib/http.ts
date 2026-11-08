import { env } from "./env";
import { sessionStore } from "./session";
import type { Session } from "./session";

type RequestOptions = RequestInit & { anonymous?: boolean };
export class ApiError extends Error {
  readonly status: number;
  constructor(status: number, message: string) { super(message); this.name = "ApiError"; this.status = status; }
}
async function errorMessage(response: Response): Promise<string> {
  const text = await response.text();
  if (text) {
    try {
      const body: unknown = JSON.parse(text);
      if (typeof body === "string") return body;
      if (body && typeof body === "object") {
        const problem = body as { title?: unknown; detail?: unknown; errors?: unknown };
        const errors = problem.errors && typeof problem.errors === "object" ? Object.values(problem.errors).flat().filter((v): v is string => typeof v === "string") : [];
        return [problem.detail, ...errors, problem.title].find(v => typeof v === "string" && v.trim()) as string || `Request failed (${response.status}).`;
      }
    } catch { if (!text.trimStart().startsWith("<")) return text; }
  }
  return `Request failed (${response.status}).`;
}
async function send(path: string, init: RequestInit): Promise<Response> {
  try { return await fetch(`${env.API_BASE_URL}${path}`, init); }
  catch (error) {
    if (error instanceof Error && error.name === "AbortError") throw error;
    throw new Error("Could not connect to the API. Check that it is running and try again.");
  }
}
const rotatedSessions = new WeakMap<Session, Session>();
let refreshing: { session: Session; promise: Promise<Session | null> } | null = null;
async function refresh(session: Session): Promise<Session | null> {
  if (refreshing?.session === session) return refreshing.promise;
  const promise = (async () => {
    const response = await send("/api/auth/refresh", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ refreshToken: session.refreshToken }) });
    if (!response.ok) {
      if (response.status === 401 && sessionStore.getSnapshot().session === session) sessionStore.set(null, "Your session has expired. Sign in again.");
      throw new ApiError(response.status, await errorMessage(response));
    }
    const tokens = await response.json() as Session;
    if (typeof tokens.accessToken !== "string" || !tokens.accessToken || typeof tokens.refreshToken !== "string" || !tokens.refreshToken) throw new Error("The API returned an invalid session.");
    if (sessionStore.getSnapshot().session !== session) {
      // A sign-out or a different sign-in happened while the token rotated.
      void send("/api/auth/logout", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ refreshToken: tokens.refreshToken }) }).catch(() => {});
      return null;
    }
    rotatedSessions.set(session, tokens);
    sessionStore.set(tokens, null, true);
    return tokens;
  })();
  refreshing = { session, promise };
  try { return await promise; } finally { if (refreshing?.promise === promise) refreshing = null; }
}
async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { anonymous = false, ...init } = options;
  const session = sessionStore.getSnapshot().session;
  const headers = new Headers(init.headers);
  if (init.body != null && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");
  if (!anonymous && session) headers.set("Authorization", `Bearer ${session.accessToken}`);
  let response = await send(path, { ...init, headers });
  if (response.status === 401 && !anonymous && session) {
    const current = sessionStore.getSnapshot().session;
    const renewed = current === session ? await refresh(session) : rotatedSessions.get(session) === current ? current : null;
    if (renewed && sessionStore.getSnapshot().session === renewed) {
      headers.set("Authorization", `Bearer ${renewed.accessToken}`);
      response = await send(path, { ...init, headers });
      if (response.status === 401 && sessionStore.getSnapshot().session === renewed) sessionStore.set(null, "Your session has expired. Sign in again.");
    }
  }
  if (!response.ok) throw new ApiError(response.status, await errorMessage(response));
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}
export const http = {
  get: <T>(path: string, options?: RequestOptions) => request<T>(path, options),
  post: <T>(path: string, body?: unknown, options?: RequestOptions) => request<T>(path, { ...options, method: "POST", body: JSON.stringify(body ?? {}) }),
  put: <T>(path: string, body?: unknown, options?: RequestOptions) => request<T>(path, { ...options, method: "PUT", body: JSON.stringify(body ?? {}) }),
  del: <T>(path: string, options?: RequestOptions) => request<T>(path, { ...options, method: "DELETE" }),
};
