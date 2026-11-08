// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import App, { ProtectedRoute } from "../src/App";
import { AuthPage } from "../src/features/auth/AuthPage";
import { sessionStore } from "../src/lib/session";

function mount(path: string) {
  return render(<MemoryRouter initialEntries={[path]}><Routes><Route element={<App />}>
    <Route path="/login" element={<AuthPage />} /><Route path="/register" element={<AuthPage register />} />
    <Route element={<ProtectedRoute />}><Route path="/items" element={<p>Private items</p>} /><Route path="/account" element={<p>Account page</p>} /></Route>
  </Route></Routes></MemoryRouter>);
}
afterEach(() => { cleanup(); sessionStore.set(null); sessionStorage.clear(); vi.unstubAllGlobals(); });
describe("authentication journey", () => {
  it("protects items, registers, persists a session and signs out", async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(new Response('{}', { status: 200 })).mockResolvedValueOnce(new Response('{"accessToken":"access","refreshToken":"refresh"}', { status: 200 })).mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    mount("/items");
    expect(screen.queryByText("Private items")).toBeNull();
    fireEvent.click(screen.getByRole("link", { name: "Create an account" }));
    fireEvent.change(screen.getByLabelText("Username"), { target: { value: "new-user" } });
    fireEvent.change(screen.getByLabelText("Email"), { target: { value: "user@example.com" } });
    fireEvent.change(screen.getByLabelText("Password"), { target: { value: "password123" } });
    fireEvent.click(screen.getByRole("button", { name: "Create account" }));
    expect(await screen.findByText("Account page")).toBeTruthy();
    expect(JSON.parse(sessionStorage.getItem("greenr.session")!).refreshToken).toBe("refresh");
    fireEvent.click(screen.getByRole("link", { name: "Items" }));
    expect(screen.getByText("Private items")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Sign out" }));
    expect(await screen.findByRole("heading", { name: "Sign in" })).toBeTruthy();
    expect(sessionStorage.getItem("greenr.session")).toBeNull();
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(3));
    expect(fetchMock.mock.calls[2][0]).toContain("/api/auth/logout");
  });
  it("announces validation and server errors", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('{"title":"Username or email already exists."}', { status: 409 }));
    vi.stubGlobal("fetch", fetchMock);
    mount("/register");
    fireEvent.change(screen.getByLabelText("Username"), { target: { value: "bad name" } });
    fireEvent.change(screen.getByLabelText("Email"), { target: { value: "user@example.com" } });
    fireEvent.change(screen.getByLabelText("Password"), { target: { value: "password123" } });
    fireEvent.click(screen.getByRole("button", { name: "Create account" }));
    expect(screen.getByRole("alert").textContent).toContain("Username must");
    fireEvent.change(screen.getByLabelText("Username"), { target: { value: "valid" } });
    fireEvent.click(screen.getByRole("button", { name: "Create account" }));
    expect((await screen.findByRole("alert")).textContent).toContain("already exists");
  });
});

it("restores a persisted session after the module is reloaded", async () => {
  sessionStorage.setItem("greenr.session", JSON.stringify({ accessToken: "saved", refreshToken: "saved-refresh" }));
  vi.resetModules();
  const restored = await import("../src/lib/session");
  expect(restored.sessionStore.getSnapshot().session?.accessToken).toBe("saved");
});
