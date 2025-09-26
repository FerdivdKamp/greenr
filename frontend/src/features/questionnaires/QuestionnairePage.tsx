import { useMemo } from "react";
import { useParams } from "react-router-dom";
import { Survey } from "survey-react-ui";
import { Model as SurveyModel } from "survey-core";
import "survey-core/survey-core.css";

import { useLatestQuestionnaire, useSubmitResponse } from "./queries";

export default function QuestionnairePage() {
  const { canonicalId = "7607e780-806f-48a3-b378-ccd2e1c502c7" } = useParams(); // e.g. /questionnaires/:canonicalId

  const {
    data: dto,
    isLoading,
    error,
  } = useLatestQuestionnaire(canonicalId);

  const submit = useSubmitResponse(dto?.id ?? "");

  const model = useMemo(() => {
    if (!dto) return null;

    let def: any = dto.definition;
    if (typeof def === "string") {
      try {
        def = JSON.parse(def);
      } catch {
        /* leave as string if parse fails */
      }
    }

    const m = new SurveyModel(def);
    m.onComplete.add(async (s) => {
      await submit.mutateAsync({
        userId: null, // fill if you have auth
        answers: s.data,
      });
    });
    return m;
  }, [dto, submit]);

  if (isLoading) return <div className="p-6">Loading questionnaire…</div>;
  if (error) return <div className="p-6 text-red-600">Failed to load questionnaire</div>;
  if (!model) return <div className="p-6">No questionnaire definition</div>;

  return (
    <div className="p-6 max-w-3xl mx-auto">
      <Survey model={model} />
      {submit.isPending && <p>Submitting…</p>}
      {submit.isError && (
        <p className="text-red-600">
          {(submit.error as Error)?.message ?? "Submit failed"}
        </p>
      )}
      {submit.isSuccess && <p className="text-green-600">Thanks! Response saved.</p>}
    </div>
  );
}