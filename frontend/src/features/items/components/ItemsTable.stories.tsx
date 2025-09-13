import type { Meta, StoryObj } from "@storybook/react";
import { ItemsTable } from "./ItemsTable";
import type { Item } from "../types";

const demo: Item[] = [
  { itemId: "1", itemName: "Laptop", useCase: "Work", price: 999.99, footprintKG: 120, dateOfPurchase: "2024-10-01" },
  { itemId: "2", itemName: "Bicycle", useCase: "Commute", price: 450, footprintKG: 25, dateOfPurchase: "2023-06-12" },
];

const meta: Meta<typeof ItemsTable> = {
  title: "Features/Items/ItemsTable",
  component: ItemsTable,
  tags: ["autodocs"],
  args: { items: demo },
  argTypes: {
    items: { control: "object" },
  },
};
export default meta;

type Story = StoryObj<typeof ItemsTable>;

export const Default: Story = {};
export const Empty: Story = { args: { items: [] } };
