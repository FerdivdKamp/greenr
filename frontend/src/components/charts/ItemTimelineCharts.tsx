import {
  LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend,
} from "recharts";
import type { Item } from "../../features/items/types";

// monthly straight-line decline from start -> today
// function linearSeries(total: number, start: Date, end: Date) {
//   const months = Math.max(
//     1,
//     (end.getFullYear() - start.getFullYear()) * 12 + (end.getMonth() - start.getMonth()) + 1
//   );
//   return Array.from({ length: months }).map((_, i) => {
//     const d = new Date(start.getFullYear(), start.getMonth() + i, 1);
//     const v = Math.max(0, total * (1 - i / (months - 1)));
//     return { date: d.toISOString().slice(0, 10), v: Math.round(v * 100) / 100 };
//   });
// }

/** Build a “smeared” monthly series:
 *  - For each month since purchase (inclusive), value = total / monthsElapsedSoFar
 *  - Month 1: total/1, Month 12: total/12, Month N: total/N
 */
function monthlySmearSeries(total: number, start: Date, end: Date) {
  const months = Math.max(
    1,
    (end.getFullYear() - start.getFullYear()) * 12 + (end.getMonth() - start.getMonth()) + 1
  );
  return Array.from({ length: months }).map((_, i) => {
    const monthIndex = i + 1; // months elapsed so far (1..N)
    const d = new Date(start.getFullYear(), start.getMonth() + i, 1);
    const v = total / monthIndex;
    return { 
      date: d.toISOString().slice(0, 10),
      month: monthIndex, 
      value: Math.round(v * 100) / 100 };
  });
}

export default function ItemTimelineCharts({ item }: { item: Item }) {
  const start = new Date(item.dateOfPurchase);
  const end = new Date();

  // const price = linearSeries(item.price, start, end).map(r => ({ date: r.date, value: r.v }));
  // const footprint = linearSeries(item.footprintKg, start, end).map(r => ({ date: r.date, value: r.v }));


  // €/month over time
  const pricePerMonth = monthlySmearSeries(item.price, start, end);
  // kg CO₂e/month over time
  const footprintPerMonth = monthlySmearSeries(item.footprintKg, start, end);

  const syncId = `item-sync-${item.itemId}`;

//   return (
//     <div className="space-y-6">
//       {/* Price chart */}
//       <div style={{ width: "100%", height: 260 }}>
//         <ResponsiveContainer>
//           <LineChart data={price} syncId={syncId} margin={{ top: 8, right: 16, bottom: 8, left: 8 }}>
//             <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,.1)" />
//             <XAxis dataKey="date" tick={{ fontSize: 12, fill: "rgba(255,255,255,.8)" }} />
//             <YAxis tick={{ fontSize: 12, fill: "rgba(255,255,255,.8)" }}
//                    label={{ value: "Price (€)", angle: -90, position: "insideLeft",
//                             style: { fill: "rgba(255,255,255,.8)" }}} />
//             <Tooltip formatter={(v: number) => `€${Number(v).toLocaleString()}`} />
//             <Legend />
//             <Line type="monotone" dataKey="value" name="Price remaining" stroke="#60a5fa" strokeWidth={2} dot={false} />
//           </LineChart>
//         </ResponsiveContainer>
//       </div>

//       {/* Footprint chart */}
//       <div style={{ width: "100%", height: 260 }}>
//         <ResponsiveContainer>
//           <LineChart data={footprint} syncId={syncId} margin={{ top: 8, right: 16, bottom: 8, left: 8 }}>
//             <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,.1)" />
//             <XAxis dataKey="date" tick={{ fontSize: 12, fill: "rgba(255,255,255,.8)" }} />
//             <YAxis tick={{ fontSize: 12, fill: "rgba(255,255,255,.8)" }}
//                    label={{ value: "Footprint (kg)", angle: -90, position: "insideLeft",
//                             style: { fill: "rgba(255,255,255,.8)" }}} />
//             <Tooltip formatter={(v: number) => `${Number(v).toLocaleString()} kg`} />
//             <Legend />
//             <Line type="monotone" dataKey="value" name="Footprint remaining" stroke="#34d399" strokeWidth={2} dot={false} />
//           </LineChart>
//         </ResponsiveContainer>
//       </div>
//     </div>
//   );
// }
return (
    <div className="space-y-6">
      {/* Price per month (decreases as you keep the item longer) */}
      <div style={{ width: "100%", height: 260 }}>
        <ResponsiveContainer>
          <LineChart data={pricePerMonth} syncId={syncId} margin={{ top: 8, right: 16, bottom: 8, left: 8 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,.1)" />
            <XAxis dataKey="date" tick={{ fontSize: 12, fill: "rgba(255,255,255,.8)" }} />
            <YAxis
              tick={{ fontSize: 12, fill: "rgba(255,255,255,.8)" }}
              label={{ value: "€ / month", angle: -90, position: "insideLeft", style: { fill: "rgba(255,255,255,.8)" } }}
            />
            <Tooltip 
              // formatter={(v: number) => `€${Number(v).toLocaleString()}/mo`}
              formatter={(v: number, _name, entry: any) => [
                `${Number(v).toLocaleString()} €/mo`,
                `Month ${entry.payload.month}`,
              ]} 
              />
            <Legend />
            <Line type="monotone" dataKey="value" name="Price per month" stroke="#60a5fa" strokeWidth={2} dot={false} />
          </LineChart>
        </ResponsiveContainer>
      </div>

      {/* Footprint per month (also decreases with time-in-use) */}
      <div style={{ width: "100%", height: 260 }}>
        <ResponsiveContainer>
          <LineChart data={footprintPerMonth} syncId={syncId} margin={{ top: 8, right: 16, bottom: 8, left: 8 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,.1)" />
            <XAxis dataKey="date" tick={{ fontSize: 12, fill: "rgba(255,255,255,.8)" }} />
            <YAxis
              tick={{ fontSize: 12, fill: "rgba(255,255,255,.8)" }}
              label={{ value: "kg CO₂e / month", angle: -90, position: "insideLeft", style: { fill: "rgba(255,255,255,.8)" } }}
            />
            <Tooltip 
              // formatter={(v: number) => `${Number(v).toLocaleString()} kg/mo`} 
              formatter={(v: number, _name, entry: any) => [
              `${Number(v).toLocaleString()} kg/mo`,
                `Month ${entry.payload.month}`,
              ]}
              />
            <Legend />
            <Line type="monotone" dataKey="value" name="Footprint per month" stroke="#34d399" strokeWidth={2} dot={false} />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
