using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace CarbonTracker.API.Contracts.Questionnaires
{
    public sealed class CreateQuestionnaireRequest
    {
        [Required] public string Title { get; set; } = "";
        public Guid? CanonicalId { get; set; }          // new family if null and no SupersedesId
        public Guid? SupersedesId { get; set; }         // bumps version in same family if set
        public string? Status { get; set; }             // null or "draft"; activation requires publish
        [Required] public JsonElement Definition { get; set; }  // SurveyJS JSON
    }
}
