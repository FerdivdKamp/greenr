import { useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Survey } from "survey-react-ui";
import { Model as SurveyModel } from "survey-core";
import "survey-core/survey-core.css";
import { useLatestQuestionnaire, useSubmitResponse } from "./queries";

export default function QuestionnairePage() {
  const { canonicalId = "" } = useParams();
  const { data: dto, isLoading, error } = useLatestQuestionnaire(canonicalId);
  const submit = useSubmitResponse(dto?.id ?? "");
  const [answers, setAnswers] = useState<Record<string, unknown> | null>(null);
  const model = useMemo(() => {
    if (!dto) return null;
    try {
      const definition = typeof dto.definition === "string" ? JSON.parse(dto.definition) : dto.definition;
      if (!definition || typeof definition !== "object" || Array.isArray(definition)) return null;
      const survey = new SurveyModel(definition);
      survey.onComplete.add(s => setAnswers(s.data));
      return survey;
    } catch { return null; }
  }, [dto]);
  async function send() { if (answers) { try { await submit.mutateAsync({ answers }); } catch { /* The error is shown below. */ } } }
  if (isLoading) return <p>Loading questionnaire…</p>;
  if (error) return <p role="alert">{error.message}</p>;
  if (!model) return <p role="alert">This questionnaire has an invalid definition.</p>;
  return <section><Link to="/questionnaires">Back to questionnaires</Link><h1>{dto?.title}</h1>{!answers && <Survey model={model} />}{answers && !submit.isSuccess && <><p>Ready to submit your answers.</p><button disabled={submit.isPending} onClick={() => void send()}>{submit.isPending ? "Submitting…" : "Submit answers"}</button>{submit.error && <p role="alert">{submit.error.message} You can try again.</p>}</>}{submit.isSuccess && <p role="status">Thank you. Your response was saved.</p>}</section>;
}
