export type QuestionnaireDto = {
  id: string;
  canonicalId: string;
  version: number;
  title: string;
  status: string;
  createdAt: string;
  updatedAt: string;
  // backend returns a JSON string or object; accept both
  definition: unknown;
};

export type SubmitResponseRequest = {
  userId?: string | null;
  answers: Record<string, unknown>;
};