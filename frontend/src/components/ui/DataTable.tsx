import React from "react";

/** Column definition for a generic row type T */
export type Column<T> = {
  /** Header text */
  header: string;
  /** Access field by key (simple render) */
  accessor?: keyof T;
  /** Or provide a custom cell renderer */
  render?: (row: T) => React.ReactNode;
  /** Optional column width */
  width?: number | string;
};

export type DataTableProps<T> = {
  data: T[];
  columns: Column<T>[];
  /** How to key each row (default: index) */
  getRowKey?: (row: T, index: number) => React.Key;
  /** Message when data is empty */
  emptyMessage?: string;
  /** Optional loading state to show a simple message */
  loading?: boolean;
};

export function DataTable<T>({
  data,
  columns,
  getRowKey,
  emptyMessage = "No data.",
  loading = false,
}: DataTableProps<T>) {
  if (loading) {
    return <p style={{ marginTop: 12 }}>Loading…</p>;
  }
  if (!data || data.length === 0) {
    return <p style={{ marginTop: 12 }}>{emptyMessage}</p>;
  }

  return (
    <table style={{ borderCollapse: "collapse", minWidth: 640, marginTop: 16 }}>
      <thead>
        <tr>
          {columns.map((col, i) => (
            <th key={i} style={{ ...th, width: col.width }}>{col.header}</th>
          ))}
        </tr>
      </thead>
      <tbody>
        {data.map((row, idx) => (
          <tr key={getRowKey ? getRowKey(row, idx) : idx}>
            {columns.map((col, i) => (
              <td key={i} style={td}>
                {col.render ? col.render(row) : (row[col.accessor as keyof T] as React.ReactNode)}
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  );
}

const th: React.CSSProperties = { textAlign: "left", borderBottom: "1px solid #ddd", padding: 8 };
const td: React.CSSProperties = { borderBottom: "1px solid #eee", padding: 8 };
