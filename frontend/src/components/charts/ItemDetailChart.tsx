import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis, CartesianGrid, Legend } from "recharts";
import type { Item } from "../../features/items/ItemsPage";

// Create monthly points from purchase to today (inclusive) with linear decline to 0.
function buildAmortizedSeries(total: number, start: Date, end: Date) {
  // get month count (at least 1)
  const months = Math.max(
    1,
    (end.getFullYear() - start.getFullYear()) * 12 + (end.getMonth() - start.getMonth()) + 1
  );

  return Array.from({ length: months }).map((_, idx) => {
    const d = new Date(start.getFullYear(), start.getMonth() + idx, 1);
    const remaining = Math.max(0, total * (1 - idx / (months - 1)));
    return { date: d.toISOString().slice(0, 10), remaining };
  });
}

export default function ItemDetailChart({
  item,
  height = 320,
}: {
  item: Item;
  height?: number;
}) {
  const start = new Date(item.purchased);
  const end = new Date();
  // price & footprint amortized to today
  const price = buildAmortizedSeries(item.price, start, end);
  const footprint = buildAmortizedSeries(item.footprintKg, start, end);

  // merge into one row per date
  const data = price.map((p, i) => ({
    date: p.date,
    priceRemaining: Math.round(p.remaining * 100) / 100,
    footprintRemaining: Math.round(footprint[i].remaining * 100) / 100,
  }));

  return (
    <div style={{ width: "100%", height }}>
      <ResponsiveContainer>
        <LineChart data={data} margin={{ top: 10, right: 16, bottom: 10, left: 8 }}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey="date" tick={{ fontSize: 12 }} />
          <YAxis tick={{ fontSize: 12 }} />
          <Tooltip />
          <Legend />
          <Line type="monotone" dataKey="priceRemaining" dot={false} />
          <Line type="monotone" dataKey="footprintRemaining" dot={false} />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
