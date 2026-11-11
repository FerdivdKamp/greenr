// @vitest-environment jsdom
import { afterEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import ItemsPage from "../src/features/items/ItemsPage";

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
it("creates and deletes items and refreshes the list and summary", async () => {
  let items: { itemId: string; itemName: string; useCase: string; price: number; footprintKg: number; dateOfPurchase: string | null }[] = [];
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    if (init?.method === "POST") { items = [{ ...JSON.parse(String(init.body)), itemId: "1" }]; return new Response(JSON.stringify(items[0]), { status: 201 }); }
    if (init?.method === "DELETE") { items = []; return new Response(null, { status: 204 }); }
    if (url.endsWith("/summary")) return new Response(JSON.stringify({ itemCount: items.length, totalPrice: 0, totalFootprintKg: 0, byUseCase: [] }), { status: 200 });
    return new Response(JSON.stringify(items), { status: 200 });
  });
  vi.stubGlobal("fetch", fetchMock);
  vi.stubGlobal("confirm", () => true);
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={client}><ItemsPage /></QueryClientProvider>);
  expect(await screen.findByText("No items found.")).toBeTruthy();
  fireEvent.change(screen.getByLabelText("Name"), { target: { value: "Bicycle" } });
  fireEvent.change(screen.getByLabelText("Use case"), { target: { value: "Travel" } });
  fireEvent.change(screen.getByLabelText("Price"), { target: { value: "10" } });
  fireEvent.change(screen.getByLabelText("Footprint (kg CO₂e)"), { target: { value: "2" } });
  fireEvent.click(screen.getByRole("button", { name: "Add item" }));
  expect(await screen.findByText("Bicycle")).toBeTruthy();
  await waitFor(() => expect(screen.getByText(/1 items/)).toBeTruthy());
  fireEvent.click(screen.getByRole("button", { name: "Delete Bicycle" }));
  expect(await screen.findByText("No items found.")).toBeTruthy();
  await waitFor(() => expect(screen.getByText(/0 items/)).toBeTruthy());
});
