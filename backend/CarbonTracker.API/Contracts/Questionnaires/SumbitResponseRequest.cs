using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace CarbonTracker.API.Contracts.Questionnaires
{
    public sealed class SubmitResponseRequest
    {
        public Guid? UserId { get; set; }
        [Required] public Dictionary<string, JsonElement> Answers { get; set; } = new();
    }
}
