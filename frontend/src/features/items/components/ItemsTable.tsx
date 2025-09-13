// import React from "react";
import type { Item } from "../types";
import { DataTable, type Column } from "../../../components/ui/DataTable";

export function ItemsTable({ items }: { items: Item[] }) {
  const columns: Column<Item>[] = [
    { header: "Id", accessor: "itemId", width: 120 },
    { header: "Name", accessor: "itemName" },
    { header: "Use case", accessor: "useCase" },
    { header: "Price", accessor: "price", width: 120 },
    { header: "Footprint (kg)", accessor: "footprintKg", width: 140 },
    { header: "Purchased", accessor: "dateOfPurchase", width: 160 },
  ];

  return (
    <DataTable<Item>
      data={items}
      columns={columns}
      getRowKey={(r) => r.itemId}
      emptyMessage="No items."
    />
  );
}
