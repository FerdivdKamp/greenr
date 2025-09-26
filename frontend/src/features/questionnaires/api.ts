import { http } from "../../lib/http";
import type { QuestionnaireDto, SubmitResponseRequest } from "./types";

export const getLatestQuestionnaire = (canonicalId: string) =>
  http.get<QuestionnaireDto>(`/api/questionnaires/${canonicalId}/latest`);

export const submitResponse = (questionnaireId: string, body: SubmitResponseRequest) =>
  http.post<{ id: string }>(`/api/questionnaires/${questionnaireId}/responses`, body);
