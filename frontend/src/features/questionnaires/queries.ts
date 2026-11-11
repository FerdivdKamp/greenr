import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getLatestQuestionnaire, submitResponse } from "./api";
import type { SubmitResponseRequest } from "./types";

export const qk = {
  latest: (canonicalId: string) =>
    ["questionnaire", "latest", canonicalId] as const,
};

export const useLatestQuestionnaire = (canonicalId: string) =>
  useQuery({
    queryKey: qk.latest(canonicalId),
    queryFn: () => getLatestQuestionnaire(canonicalId),
    enabled: !!canonicalId, // don’t run unless we have an ID
  });

export const useSubmitResponse = (questionnaireId: string) => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: SubmitResponseRequest) =>
      submitResponse(questionnaireId, body),
    onSuccess: () => client.invalidateQueries({ queryKey: ["questionnaire-history"] }),
  });
};
