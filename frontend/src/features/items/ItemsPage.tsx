// src/features/items/ItemsPage.tsx
import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { getItems } from "./api";
import Button from "../../components/ui/Button";
import PriceBarChart from "../../components/charts/PriceBarChart";
import ItemTimelineCharts from "../../components/charts/ItemTimelineCharts";
import { ItemsTable } from "./components/ItemsTable";
import type { Item } from "./types"; // <-- { itemId, itemName, useCase, price, footprintKg, dateOfPurchase }

/**
 * ItemsPage
 * - Manual fetch (Load items) so you control when data loads
 * - Use-case filter
 * - Bar chart (price per item) toggle
 * - Timeline charts (price + footprint) for ONE selected item, with dropdown
 */
export default function ItemsPage() {
  // Manual fetch → shows "Load items" button
  const { data, isFetching, error, refetch, isSuccess } = useQuery<Item[]>({
    queryKey: ["items"],
    queryFn: getItems,
    enabled: false,
  });

  // UI state
  const [useCaseFilter, setUseCaseFilter] = useState("all");
  const [showBarChart, setShowBarChart] = useState(false);
  const [showTimeline, setShowTimeline] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  // When data arrives the first time, default the timeline dropdown to the first item
  useEffect(() => {
    if (data && data.length > 0 && !selectedId) {
      setSelectedId(data[0].itemId);
    }
  }, [data, selectedId]);

  // Build distinct list of use-cases
  const useCases = useMemo(() => {
    const s = new Set<string>();
    (data ?? []).forEach(i => s.add(i.useCase));
    return ["all", ...Array.from(s).sort((a, b) => a.localeCompare(b))];
  }, [data]);

  // Apply use-case filter
  const filtered: Item[] = useMemo(() => {
    if (!data) return [];
    return useCaseFilter === "all" ? data : data.filter(i => i.useCase === useCaseFilter);
  }, [data, useCaseFilter]);

  // Resolve the item chosen for the timeline charts
  const selectedItem = useMemo(
    () => (data ?? []).find(i => i.itemId === selectedId) || null,
    [data, selectedId]
  );

  return (
    <div className="p-4 space-y-6">
      {/* Header + actions */}
      <div className="flex items-center gap-3">
        <h1 className="text-2xl font-bold">Items</h1>

        {/* Load items (manual) */}
        <Button size="sm" onClick={() => refetch()} disabled={isFetching}>
          {isFetching ? "Loading…" : isSuccess ? "Reload items" : "Load items"}
        </Button>

        {/* Toggle bar chart */}
        <Button
          size="sm"
          variant="secondary"
          onClick={() => setShowBarChart(v => !v)}
          disabled={!data || data.length === 0}
        >
          {showBarChart ? "Hide bar chart" : "Create bar chart"}
        </Button>

        {/* Toggle timeline */}
        <Button
          size="sm"
          variant="secondary"
          onClick={() => setShowTimeline(v => !v)}
          disabled={!data || data.length === 0}
        >
          {showTimeline ? "Hide item timeline" : "Show item timeline"}
        </Button>

        {error && <span className="text-red-400 text-sm">Failed to load.</span>}
      </div>

      {/* Use-case filter */}
      <div className="flex items-center gap-3">
        <label className="text-sm opacity-80">Use case</label>
        <select
          className="bg-transparent border rounded px-2 py-1"
          value={useCaseFilter}
          onChange={(e) => setUseCaseFilter(e.target.value)}
          disabled={!data}
        >
          {useCases.map(uc => (
            <option key={uc} value={uc}>{uc}</option>
          ))}
        </select>
      </div>

      {/* Bar chart: price per item (for the filtered list) */}
      {showBarChart && filtered.length > 0 && (
        <div className="bg-neutral-900/40 rounded-2xl p-4">
          <PriceBarChart
            items={filtered}                 // expects Item with itemId/itemName/price
            onBarClick={(itemId) => setSelectedId(itemId)}  // clicking a bar selects item for timeline
            height={360}
          />
        </div>
      )}

      {/* Timeline controls: choose ONE item (only when timeline is visible) */}
      {showTimeline && data && data.length > 0 && (
        <div className="flex items-center gap-3">
          <label className="text-sm opacity-80">Item</label>
          <select
            className="bg-transparent border rounded px-2 py-1"
            value={selectedId ?? ""}
            onChange={(e) => setSelectedId(e.target.value)}
          >
            {data.map(it => (
              <option key={it.itemId} value={it.itemId}>
                {it.itemName}
              </option>
            ))}
          </select>
        </div>
      )}

      {/* Two synchronized line charts: price & footprint for the selected item */}
      {showTimeline && selectedItem && (
        <div className="bg-neutral-900/40 rounded-2xl p-4">
          <ItemTimelineCharts item={selectedItem} />
        </div>
      )}

      {/* Data table always visible after first successful load */}
      {isSuccess && (
        <div className="bg-neutral-900/40 rounded-2xl p-2">
          {/* ItemsTable already uses the correct accessors (itemId, itemName, ...) */}
          <ItemsTable items={filtered} />
        </div>
      )}
    </div>
  );
}
