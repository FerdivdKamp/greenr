import { useState } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, userEvent, within } from "storybook/test";
import ItemsPage from "./ItemsPage";
import type { Item } from "./types";

function DemoPage() {
  const [client] = useState(() => new QueryClient({ defaultOptions: { queries: { retry: false } } }));
  return <QueryClientProvider client={client}><ItemsPage /></QueryClientProvider>;
}

const meta: Meta<typeof DemoPage> = {
  title: "Features/Items/Add an item",
  component: DemoPage,
  async beforeEach() {
    const originalFetch = globalThis.fetch;
    let items: Item[] = [];
    globalThis.fetch = async (input, init) => {
      const url = String(input);
      if (url.endsWith("/api/items") && init?.method === "POST") {
        const item = { ...JSON.parse(String(init.body)), itemId: "demo-phone" } as Item;
        items = [...items, item];
        return Response.json(item, { status: 201 });
      }
      if (url.endsWith("/api/items/summary")) {
        return Response.json({ itemCount: items.length, totalPrice: items.reduce((sum, item) => sum + item.price, 0), totalFootprintKg: 0, byUseCase: [] });
      }
      if (url.endsWith("/api/items")) return Response.json(items);
      return originalFetch(input, init);
    };
    return () => { globalThis.fetch = originalFetch; };
  },
};

export default meta;
type Story = StoryObj<typeof DemoPage>;

export const AddSamsungPhone: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.type(canvas.getByLabelText("Name"), "Samsung Galaxy S23");
    await userEvent.type(canvas.getByLabelText("Use case"), "Smartphone");
    await userEvent.type(canvas.getByLabelText("Price"), "250");
    await userEvent.type(canvas.getByLabelText(/Footprint/), "0");
    await userEvent.type(canvas.getByLabelText("Purchase date"), "2026-10-01");
    await userEvent.click(canvas.getByRole("button", { name: "Add item" }));
    await expect(await canvas.findByText("Samsung Galaxy S23")).toBeVisible();
    await expect(canvas.getByRole("button", { name: "Edit Samsung Galaxy S23" }).closest("li")).toHaveTextContent("2026-10-01");
    await expect(canvas.getByText(/1 items.*250 total price/)).toBeVisible();
  },
};
