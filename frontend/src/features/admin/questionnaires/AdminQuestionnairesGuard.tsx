import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { http } from "../../../lib/http";
import AdminQuestionnairesPage from "./AdminQuestionnairesPage";

export function AdminQuestionnairesGuard() {
  const account = useQuery({ queryKey: ["account"], queryFn: () => http.get<{ questionnaireAdmin: boolean }>("/api/auth/me") });
  if (account.isPending) return <p>Checking access…</p>;
  if (account.error) return <p role="alert">{account.error.message}</p>;
  if (!account.data.questionnaireAdmin) return <p role="alert">You do not have access to questionnaire administration.</p>;
  return <AdminEditor />;
}
function AdminEditor() {
  const client = useQueryClient();
  const [title, setTitle] = useState("");
  const [definition, setDefinition] = useState("");
  const [parseError, setParseError] = useState("");
  const create = useMutation({ mutationFn: (body: { title: string; definition: unknown }) => http.post("/api/questionnaires", body), onSuccess: async () => { setTitle(""); setDefinition(""); await client.invalidateQueries({ queryKey: ["admin", "questionnaires"] }); } });
  return <><h1>Questionnaire administration</h1><h2>Create a draft</h2><form onSubmit={e => { e.preventDefault(); try { const parsed: unknown = JSON.parse(definition); setParseError(""); create.mutate({ title, definition: parsed }); } catch { setParseError("Enter valid JSON for the definition."); } }}><label htmlFor="questionnaire-title">Title</label><input id="questionnaire-title" required value={title} onChange={e => setTitle(e.target.value)} /><label htmlFor="definition">SurveyJS definition (JSON)</label><textarea id="definition" required rows={10} value={definition} onChange={e => setDefinition(e.target.value)} /><button disabled={create.isPending}>Create draft</button>{parseError && <p role="alert">{parseError}</p>}{create.error && <p role="alert">{create.error.message}</p>}{create.isSuccess && <p role="status">Draft created.</p>}</form><AdminQuestionnairesPage /></>;
}
