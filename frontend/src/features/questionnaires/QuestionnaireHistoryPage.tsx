import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { http } from "../../lib/http";

type Entry = { id: string; title: string; version: number; submittedAt: string };
export default function QuestionnaireHistoryPage() {
  const history = useQuery({ queryKey: ["questionnaire-history"], queryFn: () => http.get<Entry[]>("/api/questionnaires/responses/history") });
  return <section><h1>Your responses</h1><Link to="/questionnaires">Back to questionnaires</Link>{history.isPending && <p>Loading history…</p>}{history.error && <p role="alert">{history.error.message}</p>}{history.data?.length === 0 && <p>No responses yet.</p>}<ul>{history.data?.map(entry => <li key={entry.id}>{entry.title} (version {entry.version}) — {new Date(entry.submittedAt).toLocaleString()}</li>)}</ul></section>;
}
