import { Model, QuestionTextModel } from "survey-core";

// This field represents elapsed travel time, not a clock time. Keep the stored
// HH:mm format required by the API while avoiding the mobile clock picker.
export function configureTravelDuration(survey: Model) {
  const question = survey.getQuestionByName("reis_tijd_totaal");
  if (!(question instanceof QuestionTextModel) || question.inputType !== "time") return;

  question.inputType = "text";
  question.placeholder = "HH:mm (for example, 01:30)";
  question.description = "Enter the duration in hours and minutes, from 00:00 to 23:59.";
  survey.onValidateQuestion.add((_sender, options) => {
    if (options.question !== question || options.value == null || options.value === "") return;
    if (typeof options.value !== "string" || !/^(?:[01]\d|2[0-3]):[0-5]\d$/.test(options.value)) {
      options.error = "Enter a duration in HH:mm format, such as 01:30.";
    }
  });
}
