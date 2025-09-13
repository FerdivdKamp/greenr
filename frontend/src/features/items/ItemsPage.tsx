import { useQuery } from "@tanstack/react-query";
import { getItems } from "./api";
import { ItemsTable } from "./components/ItemsTable";
import Button from "../../components/ui/Button";

export default function ItemsPage() {
  const { data, isFetching, error, refetch, isSuccess } = useQuery({
    queryKey: ["items"],
    queryFn: getItems,
    enabled: false, // click-to-fetch
  });

  return (
    <div style={{ padding: 16 }}>
      <h1>Items</h1>

      <Button onClick={() => refetch()} disabled={isFetching}>
        {isFetching ? "Loading…" : "Load items"}
      </Button>

      {error && (
        <p style={{ color: "crimson", marginTop: 12 }}>
          {(error as Error).message}
        </p>
      )}

      {isSuccess && data && <ItemsTable items={data} />}
    </div>
  );
}
