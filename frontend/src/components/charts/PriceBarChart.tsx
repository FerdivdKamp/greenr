// src/components/charts/PriceBarChart.tsx
import {
  BarChart, Bar, XAxis, YAxis, Tooltip, CartesianGrid, ResponsiveContainer, Cell,
} from "recharts";
import type { Item } from "../../features/items/types";

const COLORS = ["#60a5fa","#f87171","#34d399","#fbbf24","#a78bfa","#f472b6","#38bdf8","#c084fc","#fb923c","#4ade80"];

export default function PriceBarChart({
  items, onBarClick, height = 360,
}: { items: Item[]; onBarClick?: (id: string) => void; height?: number; }) {

  const data = (items ?? []).map((i, idx) => ({
    id: i.itemId ?? String(idx),
    label: i.itemName ?? `Item ${idx + 1}`,
    price: typeof i.price === "string" ? Number(i.price) : i.price,
  }));

  return (
    <div style={{ width: "100%", height }}>
      <ResponsiveContainer>
        <BarChart data={data} margin={{ top: 8, right: 12, bottom: 48, left: 12 }} barCategoryGap="20%">
          <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,.12)" vertical={false} />
          <XAxis dataKey="label" interval={0} angle={-20} textAnchor="end" height={56}
                 tick={{ fontSize: 12, fill: "rgba(255,255,255,.85)" }} />
          <YAxis tick={{ fontSize: 12, fill: "rgba(255,255,255,.85)" }}
                 label={{ value: "Price (€)", angle: -90, position: "insideLeft",
                 style: { fill: "rgba(255,255,255,.85)" } }} />
          <Tooltip cursor={false}
                   contentStyle={{ background:"rgba(40, 40, 44, 0.95)", border:"1px solid rgba(255,255,255,.12)", borderRadius:8, padding:"6px 10px" }}
                   formatter={(v: number) => [
                      <span style={{ color: "#f8f8f8ff", fontWeight: 600 }}>€{v.toLocaleString()}</span>, // 👈 price in accent color
                    ]}
                   />
          <Bar dataKey="price" radius={[6,6,0,0]}>
            {data.map((row, i) => (
              <Cell key={`cell-${row.id}`} fill={COLORS[i % COLORS.length]} cursor="pointer"
                    onClick={() => onBarClick?.(row.id)} />
            ))}
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
