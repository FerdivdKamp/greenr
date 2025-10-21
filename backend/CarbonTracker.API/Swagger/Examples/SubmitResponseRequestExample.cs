using CarbonTracker.API.Contracts.Questionnaires;
using Swashbuckle.AspNetCore.Filters;
using System.Text.Json;

public sealed class SubmitResponseRequestExample : IExamplesProvider<SubmitResponseRequest>
{
    public SubmitResponseRequest GetExamples() => new()
    {
        UserId = null, // or Guid if you have authentication
        Answers = JsonDocument.Parse("""
        {
          "dagen_naar_werk": 0,
          "vervoer": ["public_transport"],
          "reis_tijd_totaal": "00:30"
        }
        """).RootElement
    };
}
