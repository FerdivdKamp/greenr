namespace CarbonTracker.API.Contracts.Questionnaires
{
    public sealed record QuestionnaireDto(
        Guid Id,
        Guid CanonicalId,
        int Version,
        string Title,
        string Status,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        string Definition // return JSON as string; or JsonElement if you prefer
    );
}
