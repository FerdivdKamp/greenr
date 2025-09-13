import type { Meta, StoryObj } from "@storybook/react";
import { DataTable, type Column } from "./DataTable";

type Row = {
  id: string;
  name: string;
  price: number;
  purchased: string;
};

const rows: Row[] = [
  { id: "1", name: "Laptop", price: 999.99, purchased: "2024-10-01" },
  { id: "2", name: "Bicycle", price: 450.0, purchased: "2023-06-12" },
];

const cols: Column<Row>[] = [
  { header: "Id", accessor: "id", width: 120 },
  { header: "Name", accessor: "name" },
  { header: "Price", accessor: "price", width: 140 },
  { header: "Purchased", accessor: "purchased", width: 160 },
];

const meta: Meta<typeof DataTable<Row>> = {
  title: "UI/DataTable",
  component: DataTable,
  tags: ["autodocs"],
  args: {
    data: rows,
    columns: cols,
  },
  argTypes: {
    data: { control: "object" },
    columns: { control: false }, // columns are usually static per usage
    loading: { control: "boolean" },
    emptyMessage: { control: "text" },
  },
};
export default meta;

type Story = StoryObj<typeof DataTable<Row>>;

export const Default: Story = {};
export const Loading: Story = { args: { loading: true } };
export const Empty: Story = { args: { data: [] } };
