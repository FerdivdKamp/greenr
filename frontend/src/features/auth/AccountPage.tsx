import { useQuery } from "@tanstack/react-query";
import { http } from "../../lib/http";
export function AccountPage() {
  const { data, error, isPending } = useQuery({ queryKey: ["account"], queryFn: () => http.get<{ username: string; userId: string }>("/api/auth/me"), retry: false });
  return <section><h1>Account</h1>{isPending && <p>Loading account…</p>}{error && <p role="alert">{error.message}</p>}{data && <p>Signed in as <strong>{data.username}</strong>.</p>}</section>;
}
