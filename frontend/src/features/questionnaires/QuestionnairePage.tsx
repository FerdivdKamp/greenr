import { useMemo, useState, useCallback } from "react";
import { Model as SurveyModel } from "survey-core";
import { Survey } from "survey-react-ui";
import "survey-core/survey-core.css";

// TODO: replace this with a fetch to your backend later
const surveyJson = {
  "title": "Woon-werk verkeer",
  "logo": "https://surveyjs.io/Content/Images/examples/logo.png",
  "logoHeight": "60px",
  "completedHtml": "<h3>Thank you for your feedback</h3>",
  "pages": [
    {
      "name": "page1",
      "title": "Woon-werk verkeer",
      "elements": [
        {
          "type": "rating",
          "name": "dagen_naar_werk",
          "title": "Hoevaak per week ga je naar werk",
          "isRequired": true,
          "rateCount": 8,
          "rateMin": 0,
          "rateMax": 7,
          "maxRateDescription": "Dagen van de week"
        },
        {
          "type": "checkbox",
          "name": "vervoer",
          "visibleIf": "{dagen_naar_werk} >= 1", // note: your previous 9 would never show
          "title": "Welk vervoersmiddel gebruik je daarvoor?",
          "isRequired": true,
          "validators": [{ "type": "answercount", "maxCount": 3 }],
          "choices": [
            { "value": "public_transport", "text": "OV (trein, bus, metro)" },
            { "value": "car", "text": "Auto" },
            { "value": "scooter", "text": "Scooter / Brommer" },
            { "value": "e-bike", "text": "Elektrische fiets" },
            { "value": "bike", "text": "Fiets" },
            { "value": "walk", "text": "Lopend" }
          ],
          "colCount": 2
        },
        {
          "type": "text",
          "name": "reis_tijd_totaal",
          "title": "Hoelang is je reistijd (heen en terug opgeteld)",
          "inputType": "time",
          "min": "00:00",
          "max": "23:59"
        }
      ]
    }
  ],
  "headerView": "advanced"
};

export default function QuestionnairePage() {
  // If you want to load from backend: fetch JSON and pass into useMemo instead of surveyJson
  const model = useMemo(() => new SurveyModel(surveyJson), []);

  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const handleComplete = useCallback(async () => {
    setSubmitting(true);
    setSubmitError(null);

    try {
      // Replace with your real questionnaireId from backend
      const questionnaireId = "00000000-0000-0000-0000-000000000000";

      const payload = {
        userId: null,
        answers: model.data // key/value by question name
      };

      // POST to your API (match your ResponsesController route)
      const res = await fetch(`/api/questionnaires/${questionnaireId}/responses`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });

      if (!res.ok) {
        const text = await res.text();
        throw new Error(text || `HTTP ${res.status}`);
      }
    } catch (e: any) {
      setSubmitError(e.message ?? "Submit failed");
    } finally {
      setSubmitting(false);
    }
  }, [model]);

  // SurveyJS fires onComplete when the 'Complete' button is pressed
  model.onComplete.add(handleComplete);

  return (
    <div className="p-6 max-w-3xl mx-auto">
      <Survey model={model} />
      {submitting && <p>Submitting…</p>}
      {submitError && <p style={{ color: "red" }}>{submitError}</p>}
    </div>
  );
}
