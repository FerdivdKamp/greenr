// @vitest-environment jsdom
import { afterEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { routes } from "../src/app/router";
import { sessionStore } from "../src/lib/session";

function mount(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  render(<QueryClientProvider client={client}><RouterProvider router={router} /></QueryClientProvider>);
  return router;
}
afterEach(() => { cleanup(); sessionStore.set(null); sessionStorage.clear(); vi.unstubAllGlobals(); });

it("redirects anonymous visitors from lazy protected routes", async () => {
  const router = mount("/questionnaires/example");
  expect(await screen.findByRole("heading", { name: "Sign in" })).toBeTruthy();
  expect(router.state.location.pathname).toBe("/login");
  expect(router.state.location.state.from).toBe("/questionnaires/example");
});

it("navigates to lazy items and questionnaire pages for a signed-in user", async () => {
  sessionStore.set({ accessToken: "access", refreshToken: "refresh" });
  vi.stubGlobal("fetch", vi.fn((url: string) => {
    const data = url.endsWith("/summary") ? { itemCount: 0, totalPrice: 0, totalFootprintKg: 0, byUseCase: [] } : url.endsWith("/items") ? [] : { id: "1", title: "Broken", definition: "{" };
    return Promise.resolve(new Response(JSON.stringify(data), { status: 200 }));
  }));
  const router = mount("/");
  fireEvent.click(screen.getByRole("link", { name: "Items" }));
  expect(await screen.findByRole("heading", { name: "Your items" })).toBeTruthy();
  await router.navigate("/questionnaires/example");
  expect((await screen.findByRole("alert")).textContent).toContain("invalid definition");
});
