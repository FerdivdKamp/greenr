using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace CarbonTracker.API.Contracts.Questionnaires
{
    public sealed class CreateQuestionnaireRequest
    {
        [Required] public string Title { get; set; } = "";
        public Guid? CanonicalId { get; set; }          // new family if null and no SupersedesId
        public Guid? SupersedesId { get; set; }         // bumps version in same family if set
        public string? Status { get; set; }             // "draft" | "active" | "inactive" (defaults server-side)
        [Required] public JsonElement Definition { get; set; }  // SurveyJS JSON
    }
}
