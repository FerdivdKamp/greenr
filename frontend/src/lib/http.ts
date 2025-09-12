// A small wrapper around fetch() to simplify making API calls.
// Benefits:
// - Prefixes all requests with our API base URL
// - Ensures JSON parsing
// - Throws clear errors if the response is not ok

import { requireEnv } from "./env";

const { API_BASE_URL } = requireEnv();

/**
 * Make a request and parse JSON.
 * - Prefixes path with API_BASE_URL
 * - Adds JSON headers (unless overridden)
 * - Throws on non-2xx responses
 */
async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE_URL}${path}`, {
    headers: { "Content-Type": "application/json", ...(init?.headers ?? {}) },
    ...init,
  });

  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new Error(`${res.status} ${res.statusText}${text ? `: ${text}` : ""}`);
  }

  // Handle empty body gracefully (e.g., 204 No Content)
  if (res.status === 204) return undefined as unknown as T;

  return (await res.json()) as T;
}

export const http = {
  get:  <T>(p: string, init?: RequestInit) => request<T>(p, init),
  post: <T>(p: string, body?: unknown, init?: RequestInit) =>
    request<T>(p, { method: "POST", body: JSON.stringify(body ?? {}), ...init }),
  put:  <T>(p: string, body?: unknown, init?: RequestInit) =>
    request<T>(p, { method: "PUT", body: JSON.stringify(body ?? {}), ...init }),
  del:  <T>(p: string, init?: RequestInit) =>
    request<T>(p, { method: "DELETE", ...init }),
};

