using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace CarbonTracker.API.Contracts.Questionnaires
{
    public sealed class SubmitResponseRequest
    {
        [Required] 
        public JsonElement Answers { get; set; }
    }
}
