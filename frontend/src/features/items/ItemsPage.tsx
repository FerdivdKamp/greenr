import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { getItems } from "./api";
import PriceBarChart from "../../components/charts/PriceBarChart";
import ItemDetailChart from "../../components/charts/ItemDetailChart";
import Button from "../../components/ui/Button";
import { ItemsTable } from "./components/ItemsTable";

export type Item = {
  id: string;
  name: string;
  useCase: string;
  price: number;
  footprintKg: number;
  purchased: string; // ISO date
};

export default function ItemsPage() {
  // manual fetch so we have a "Load items" button
  const { data, isFetching, error, refetch, isSuccess } = useQuery<Item[]>({
    queryKey: ["items"],
    queryFn: getItems,
    enabled: false,
  });

  const [useCaseFilter, setUseCaseFilter] = useState("all");
  const [selected, setSelected] = useState<Item | null>(null);
  const [showChart, setShowChart] = useState(false);

  const useCases = useMemo(() => {
    const s = new Set<string>();
    (data ?? []).forEach((i) => s.add(i.useCase));
    return ["all", ...Array.from(s).sort((a, b) => a.localeCompare(b))];
  }, [data]);

  const filtered = useMemo(() => {
    if (!data) return [];
    return useCaseFilter === "all"
      ? data
      : data.filter((i) => i.useCase === useCaseFilter);
  }, [data, useCaseFilter]);

  return (
    <div className="p-4 space-y-6">
      <div className="flex items-center gap-3">
        <h1 className="text-xl font-semibold">Items</h1>

        {/* Load items */}
        <Button size="sm" onClick={() => refetch()} disabled={isFetching}>
          {isFetching ? "Loading…" : isSuccess ? "Reload items" : "Load items"}
        </Button>

        {/* Toggle chart */}
        <Button
          size="sm"
          variant="secondary"
          onClick={() => setShowChart((v) => !v)}
          disabled={!data || data.length === 0}
        >
          {showChart ? "Hide chart" : "Create bar chart"}
        </Button>

        {error && (
          <span className="text-red-400 text-sm">Failed to load.</span>
        )}
      </div>

      {/* Filter */}
      <div className="flex items-center gap-3">
        <label className="text-sm opacity-80">Use case</label>
        <select
          className="bg-transparent border rounded px-2 py-1"
          value={useCaseFilter}
          onChange={(e) => setUseCaseFilter(e.target.value)}
          disabled={!data}
        >
          {useCases.map((uc) => (
            <option key={uc} value={uc}>
              {uc}
            </option>
          ))}
        </select>
      </div>

      {/* Chart (optional) */}
      {showChart && filtered.length > 0 && (
        <div className="bg-neutral-900/40 rounded-2xl p-4">
          <PriceBarChart
            items={filtered}
            onBarClick={(itemId) => {
              const it = filtered.find((i) => i.id === itemId) ?? null;
              setSelected(it);
            }}
            height={360}
          />
        </div>
      )}

      {/* ✅ Data table is back */}
      {isSuccess && (
        <div className="bg-neutral-900/40 rounded-2xl p-2">
          <ItemsTable
            items={filtered}
            loading={isFetching}              // if your table prop is `isLoading`, rename here
            onRowClick={(it: Item) => setSelected(it)} // optional: open detail on row click
          />
        </div>
      )}

      {/* Detail overlay */}
      {selected && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-neutral-900 p-4 rounded-2xl w-[min(92vw,900px)]">
            <div className="flex items-center justify-between mb-3">
              <h2 className="text-lg font-semibold">{selected.name}</h2>
              <Button
                size="sm"
                variant="secondary"
                onClick={() => setSelected(null)}
              >
                Close
              </Button>
            </div>
            <ItemDetailChart item={selected} height={360} />
          </div>
        </div>
      )}
    </div>
  );
}
