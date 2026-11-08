import { afterEach, describe, expect, it, vi } from "vitest";
import { parseApiBaseUrl } from "../src/lib/env";
import { http } from "../src/lib/http";
import { sessionStore } from "../src/lib/session";

afterEach(() => {
  sessionStore.set(null);
  vi.unstubAllGlobals();
});

describe("API configuration", () => {
  it("accepts an empty proxy setting or an HTTP origin", () => {
    expect(parseApiBaseUrl("")).toBe("");
    expect(parseApiBaseUrl("http://localhost:5285/")).toBe("http://localhost:5285");
  });
  it.each(["localhost:5285", "ftp://example.com", "https://user:pass@example.com", "https://example.com/api", "https://example.com/?key=secret"])("rejects invalid origin %s", value => {
    expect(() => parseApiBaseUrl(value)).toThrow("VITE_API_BASE_URL");
  });
});

describe("API requests", () => {
  it("sends the access token and preserves supplied headers", async () => {
    sessionStore.set({ accessToken: "access", refreshToken: "refresh" });
    const fetchMock = vi.fn().mockResolvedValue(new Response("[]", { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);
    await http.get("/api/items", { headers: new Headers({ "X-Request": "test" }) });
    const headers = fetchMock.mock.calls[0][1].headers as Headers;
    expect(headers.get("Authorization")).toBe("Bearer access");
    expect(headers.get("X-Request")).toBe("test");
  });

  it("clears the current session on a protected 401", async () => {
    sessionStore.set({ accessToken: "access", refreshToken: "refresh" });
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response('{"title":"Unauthorized"}', { status: 401 })));
    await expect(http.get("/api/items")).rejects.toThrow("Unauthorized");
    expect(sessionStore.getSnapshot().session).toBeNull();
    expect(sessionStore.getSnapshot().message).toContain("Sign in again");
  });

  it("does not sign out a newer session when an older request fails", async () => {
    sessionStore.set({ accessToken: "old", refreshToken: "old" });
    let respond!: (response: Response) => void;
    vi.stubGlobal("fetch", vi.fn(() => new Promise<Response>(resolve => { respond = resolve; })));
    const pending = http.get("/api/items");
    sessionStore.set({ accessToken: "new", refreshToken: "new" });
    respond(new Response("", { status: 401 }));
    await expect(pending).rejects.toThrow();
    expect(sessionStore.getSnapshot().session?.accessToken).toBe("new");
  });

  it("surfaces validation and network errors and keeps anonymous login failures separate", async () => {
    sessionStore.set({ accessToken: "access", refreshToken: "refresh" });
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(new Response('{"title":"Invalid request","errors":{"name":["Name is required."]}}', { status: 400 }))
      .mockResolvedValueOnce(new Response('{"title":"Invalid credentials"}', { status: 401 }))
      .mockRejectedValueOnce(new TypeError("offline")));
    await expect(http.post("/api/items", {})).rejects.toThrow("Name is required.");
    await expect(http.post("/api/auth/login", {}, { anonymous: true })).rejects.toThrow("Invalid credentials");
    expect(sessionStore.getSnapshot().session?.accessToken).toBe("access");
    await expect(http.get("/api/items")).rejects.toThrow("Could not connect to the API");
  });
});
