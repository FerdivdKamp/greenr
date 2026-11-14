import { expect, it } from "vitest";
import { Model, QuestionTextModel } from "survey-core";
import { configureTravelDuration } from "../src/features/questionnaires/duration";

it("accepts a typed travel duration and rejects invalid values", () => {
  const survey = new Model({ elements: [{ type: "text", name: "reis_tijd_totaal", inputType: "time" }, { type: "text", name: "meeting", inputType: "time" }] });
  configureTravelDuration(survey);
  const duration = survey.getQuestionByName("reis_tijd_totaal") as QuestionTextModel;
  expect(duration.inputType).toBe("text");
  expect((survey.getQuestionByName("meeting") as QuestionTextModel).inputType).toBe("time");
  duration.value = "01:30";
  expect(survey.validate()).toBe(true);
  duration.value = "25:90";
  expect(survey.validate()).toBe(false);
});
