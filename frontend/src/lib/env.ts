/** Vite variables are public build-time configuration, never secrets. */
export function parseApiBaseUrl(value: string | undefined): string {
  const input = value?.trim() ?? "";
  if (!input) return ""; // Same origin; Vite proxies /api in development.
  let url: URL;
  try {
    url = new URL(input);
  } catch {
    throw new Error("VITE_API_BASE_URL must be an absolute HTTP or HTTPS origin, or empty for the same origin.");
  }
  if (!(["http:", "https:"].includes(url.protocol)) || url.username || url.password || url.pathname !== "/" || url.search || url.hash) {
    throw new Error("VITE_API_BASE_URL must be an HTTP or HTTPS origin without credentials, a path, query, or fragment.");
  }
  return url.origin;
}

export const env = { API_BASE_URL: parseApiBaseUrl(import.meta.env.VITE_API_BASE_URL) } as const;
