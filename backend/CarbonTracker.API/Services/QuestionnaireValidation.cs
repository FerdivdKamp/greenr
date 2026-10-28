using System.Text.Json;

namespace CarbonTracker.API.Services;

// A deliberately small SurveyJS contract. Unsupported rules fail at authoring time
// rather than giving authors the impression they will be enforced on submission.
public static class QuestionnaireValidation
{
    private static readonly HashSet<string> Types = ["text", "comment", "boolean", "rating", "radiogroup", "dropdown", "checkbox"];
    private static readonly HashSet<string> Rules = ["visibleIf", "requiredIf", "enableIf", "validators", "expression", "choicesByUrl", "choicesFromQuestion", "showOtherItem", "showNoneItem", "showSelectAllItem", "valueName", "defaultValue", "defaultValueExpression", "rateValues", "rateStep", "valueTrue", "valueFalse", "calculatedValues", "triggers"];

    private static IEnumerable<JsonElement> Questions(JsonElement definition) =>
        definition.TryGetProperty("elements", out var elements)
            ? elements.EnumerateArray()
            : definition.GetProperty("pages").EnumerateArray().SelectMany(p => p.GetProperty("elements").EnumerateArray());

    public static string? ValidateDefinition(JsonElement definition)
    {
        if (definition.ValueKind != JsonValueKind.Object) return "Definition must be a JSON object.";
        try
        {
            // Reject conditional/custom rules anywhere, including at page level.
            if (HasUnsupportedRule(definition)) return "Conditional logic, validators, dynamic choices and custom rules are unsupported.";
            if (definition.TryGetProperty("elements", out _) == definition.TryGetProperty("pages", out _))
                return "Use either elements or pages containing elements.";
            var questions = Questions(definition).ToArray();
            if (questions.Length == 0) return "At least one question is required.";
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var q in questions)
            {
                var name = q.GetProperty("name").GetString();
                var type = q.GetProperty("type").GetString()!;
                if (string.IsNullOrWhiteSpace(name) || !names.Add(name)) return "Question names must be nonempty and unique.";
                if (!Types.Contains(type)) return "Unsupported question type.";
                if (q.TryGetProperty("isRequired", out var required)) _ = required.GetBoolean();
                if (q.TryGetProperty("inputType", out var input) && input.GetString() is not ("text" or "number" or "time"))
                    return "Supported inputType values are text, number and time.";
                foreach (var key in new[] { "min", "max", "rateMin", "rateMax", "maxLength" })
                    if (q.TryGetProperty(key, out var bound) && !bound.TryGetDecimal(out _))
                        return "Bounds must be numeric; time bounds are unsupported.";
                if (Number(q, "min") > Number(q, "max") || Number(q, "rateMin") > Number(q, "rateMax"))
                    return "Minimum cannot exceed maximum.";
                if (type is "radiogroup" or "dropdown" or "checkbox")
                {
                    var choices = q.GetProperty("choices").EnumerateArray().Select(Choice).ToArray();
                    if (choices.Length == 0 || choices.Any(c => c.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
                        || choices.Select(c => c.ToString()).Distinct().Count() != choices.Length)
                        return "Choices must have unique string or numeric values.";
                }
            }
            return null;
        }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or FormatException)
        {
            return "Malformed questionnaire structure or question rules.";
        }
    }

    private static bool HasUnsupportedRule(JsonElement node) => node.ValueKind switch
    {
        JsonValueKind.Object => node.EnumerateObject().Any(p => Rules.Contains(p.Name) || HasUnsupportedRule(p.Value)),
        JsonValueKind.Array => node.EnumerateArray().Any(HasUnsupportedRule),
        _ => false
    };

    private static JsonElement Choice(JsonElement c) => c.ValueKind == JsonValueKind.Object ? c.GetProperty("value") : c;
    private static decimal? Number(JsonElement q, string key) => q.TryGetProperty(key, out var n) ? n.GetDecimal() : null;
    private static bool Empty(JsonElement value) => value.ValueKind == JsonValueKind.Null
        || (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()))
        || (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0);

    public static string? ValidateAnswers(JsonElement definition, JsonElement answers)
    {
        if (ValidateDefinition(definition) is not null) return "This definition is unsupported; an administrator must publish a supported version.";
        if (answers.ValueKind != JsonValueKind.Object) return "Answers must be a JSON object.";
        var questions = Questions(definition).ToDictionary(q => q.GetProperty("name").GetString()!);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var answer in answers.EnumerateObject())
        {
            if (!seen.Add(answer.Name) || !questions.ContainsKey(answer.Name)) return "Unknown or duplicate answer name.";
        }
        foreach (var (name, q) in questions)
        {
            var type = q.GetProperty("type").GetString();
            var supplied = answers.TryGetProperty(name, out var value);
            if (!supplied || Empty(value))
            {
                if (q.TryGetProperty("isRequired", out var required) && required.GetBoolean()) return $"Answer required: {name}.";
                if (!supplied || value.ValueKind == JsonValueKind.Null
                    || (type == "checkbox" && value.ValueKind == JsonValueKind.Array)
                    || (type is "text" or "comment" && value.ValueKind == JsonValueKind.String)) continue;
            }
            bool valid;
            if (type is "radiogroup" or "dropdown" or "checkbox")
            {
                var choices = q.GetProperty("choices").EnumerateArray().Select(Choice).ToArray();
                bool Allowed(JsonElement v) => choices.Any(c => c.ValueKind == v.ValueKind && c.ToString() == v.ToString());
                valid = type == "checkbox"
                    ? value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(Allowed)
                        && value.EnumerateArray().Select(v => v.ToString()).Distinct().Count() == value.GetArrayLength()
                    : Allowed(value);
            }
            else if (type == "rating" || (q.TryGetProperty("inputType", out var input) && input.GetString() == "number"))
            {
                valid = value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var n)
                    && n >= (Number(q, type == "rating" ? "rateMin" : "min") ?? (type == "rating" ? 1 : decimal.MinValue))
                    && n <= (Number(q, type == "rating" ? "rateMax" : "max") ?? (type == "rating" ? 5 : decimal.MaxValue))
                    && (type != "rating" || n == decimal.Truncate(n));
            }
            else if (type == "boolean") valid = value.ValueKind is JsonValueKind.True or JsonValueKind.False;
            else
            {
                valid = value.ValueKind == JsonValueKind.String
                    && value.GetString()!.Length <= (Number(q, "maxLength") ?? int.MaxValue);
                if (valid && q.TryGetProperty("inputType", out var time) && time.GetString() == "time")
                    valid = TimeOnly.TryParseExact(value.GetString(), "HH:mm", out _);
            }
            if (!valid) return $"Invalid answer: {name}.";
        }
        return null;
    }
}
