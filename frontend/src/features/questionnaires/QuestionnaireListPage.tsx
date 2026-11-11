import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { http } from "../../lib/http";
import type { QuestionnaireDto } from "./types";

export default function QuestionnaireListPage() {
  const account = useQuery({ queryKey: ["account"], queryFn: () => http.get<{ questionnaireAdmin: boolean }>("/api/auth/me") });
  const list = useQuery({ queryKey: ["questionnaires"], queryFn: () => http.get<QuestionnaireDto[]>("/api/questionnaires") });
  return <section><h1>Questionnaires</h1><p><Link to="/questionnaires/history">Your responses</Link></p>{account.data?.questionnaireAdmin && <p><Link to="/questionnaires/admin">Manage questionnaires</Link></p>}{list.isPending && <p>Loading questionnaires…</p>}{list.error && <p role="alert">{list.error.message}</p>}{list.data && !list.data.some(q => q.status === "active") && <p>No published questionnaires are available.</p>}<ul>{list.data?.filter(q => q.status === "active").map(q => <li key={q.id}><Link to={`/questionnaires/${q.canonicalId}`}>{q.title}</Link> (version {q.version})</li>)}</ul></section>;
}

