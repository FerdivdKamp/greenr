import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { http } from "../../../lib/http";
import type { QuestionnaireDto } from "../../questionnaires/types";

// reuse existing read endpoint
const getAll = () => http.get<QuestionnaireDto[]>("/api/questionnaires");

// canonical publish (latest)
const publishLatestInFamily = (canonicalId: string) =>
  http.post<void>(`/api/questionnaires/families/${canonicalId}/publish`, { latest: true });

export const useAllQuestionnaires = () =>
  useQuery({ queryKey: ["admin","questionnaires","all"], queryFn: getAll });

export const usePublishLatestInFamily = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: publishLatestInFamily,
    onSuccess: (_,_canonicalId) => {
      // refresh admin list + any consumer of "latest"
      qc.invalidateQueries({ queryKey: ["admin","questionnaires","all"] });
      qc.invalidateQueries({ predicate: (q) => (q.queryKey as any[])[0] === "questionnaire" });
    }
  });
};
