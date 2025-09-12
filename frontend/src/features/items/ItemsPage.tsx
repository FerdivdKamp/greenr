import { useQuery } from "@tanstack/react-query";
import { getItems } from "./api";

export default function ItemsPage() {
  const { data, isFetching, error, refetch, isSuccess } = useQuery({
    queryKey: ["items"],
    queryFn: getItems,
    enabled: false, // fetch on demand via button
  });

  return (
    <div style={{ padding: 16 }}>
      <h1>Items</h1>

      <button onClick={() => refetch()} disabled={isFetching}>
        {isFetching ? "Loading…" : "Load items"}
      </button>

      {error && (
        <p style={{ color: "crimson", marginTop: 12 }}>
          {(error as Error).message}
        </p>
      )}

      {isSuccess && data && data.length > 0 && (
        <table style={{ marginTop: 16, borderCollapse: "collapse", minWidth: 720 }}>
          <thead>
            <tr>
              <th style={th}>Id</th>
              <th style={th}>Name</th>
              <th style={th}>Use case</th>
              <th style={th}>Price</th>
              <th style={th}>Footprint (kg)</th>
              <th style={th}>Purchased</th>
            </tr>
          </thead>
          <tbody>
            {data.map((x) => (
              <tr key={x.itemId}>
                <td style={td}>{x.itemId}</td>
                <td style={td}>{x.itemName}</td>
                <td style={td}>{x.useCase}</td>
                <td style={td}>{x.price}</td>
                <td style={td}>{x.footprintKG}</td>
                <td style={td}>{x.dateOfPurchase}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {isSuccess && (!data || data.length === 0) && (
        <p style={{ marginTop: 12 }}>No items.</p>
      )}
    </div>
  );
}

const th: React.CSSProperties = { textAlign: "left", borderBottom: "1px solid #ddd", padding: 8 };
const td: React.CSSProperties = { borderBottom: "1px solid #eee", padding: 8 };
