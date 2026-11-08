import { env } from "./env";
import { sessionStore } from "./session";

type RequestOptions = RequestInit & { anonymous?: boolean };

export class ApiError extends Error {
  readonly status: number;
  constructor(status: number, message: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

async function errorMessage(response: Response): Promise<string> {
  const text = await response.text();
  if (text) {
    try {
      const body: unknown = JSON.parse(text);
      if (typeof body === "string") return body;
      if (body && typeof body === "object") {
        const problem = body as { title?: unknown; detail?: unknown; errors?: unknown };
        const errors = problem.errors && typeof problem.errors === "object"
          ? Object.values(problem.errors).flat().filter((value): value is string => typeof value === "string")
          : [];
        return [problem.detail, ...errors, problem.title].find(value => typeof value === "string" && value.trim()) as string || `Request failed (${response.status}).`;
      }
    } catch {
      if (!text.trimStart().startsWith("<")) return text;
    }
  }
  return `Request failed (${response.status}).`;
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { anonymous = false, ...init } = options;
  const session = sessionStore.getSnapshot().session;
  const headers = new Headers(init.headers);
  if (init.body != null && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");
  if (!anonymous && session) headers.set("Authorization", `Bearer ${session.accessToken}`);

  let response: Response;
  try {
    response = await fetch(`${env.API_BASE_URL}${path}`, { ...init, headers });
  } catch (error) {
    if (error instanceof Error && error.name === "AbortError") throw error;
    throw new Error("Could not connect to the API. Check that it is running and try again.");
  }
  if (!response.ok) {
    if (response.status === 401 && !anonymous && sessionStore.getSnapshot().session === session) {
      sessionStore.set(null, "Your session has expired or you are signed out. Sign in again.");
    }
    throw new ApiError(response.status, await errorMessage(response));
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const http = {
  get: <T>(path: string, options?: RequestOptions) => request<T>(path, options),
  post: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(path, { ...options, method: "POST", body: JSON.stringify(body ?? {}) }),
  put: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(path, { ...options, method: "PUT", body: JSON.stringify(body ?? {}) }),
  del: <T>(path: string, options?: RequestOptions) => request<T>(path, { ...options, method: "DELETE" }),
};
