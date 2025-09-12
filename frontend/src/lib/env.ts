/**
 * Public runtime configuration for the frontend.
 * Values come from `import.meta.env` (Vite) and safe fallbacks.
 */
export interface FrontendEnv {
  /**
   * Base URL used by the HTTP client for API requests.
   * In development this typically remains `"/api"` and is rewritten by Vite's proxy.
   * In production you can set it via `VITE_API_BASE_URL` (e.g. `https://api.example.com`).
   * @defaultValue "/api"
   * @example
   * // .env
   * // VITE_API_BASE_URL=https://api.greenr.dev
   */
  readonly API_BASE_URL: string;
}

/**
 * Resolved, strongly-typed environment values.
 */
export const env: FrontendEnv = {
  // API_BASE_URL: import.meta.env.VITE_API_BASE_URL ?? "/api",
  API_BASE_URL: import.meta.env.VITE_API_BASE_URL ?? "",
} as const;

/**
 * Optional helper that throws if a required value is missing.
 * Useful if later you add required vars (e.g. analytics key) and want early failure.
 */
export function requireEnv(): FrontendEnv {
  // Example of a required variable in the future:
  // if (!import.meta.env.VITE_SOME_REQUIRED_KEY) {
  //   throw new Error("Missing VITE_SOME_REQUIRED_KEY");
  // }
  return env;
}
