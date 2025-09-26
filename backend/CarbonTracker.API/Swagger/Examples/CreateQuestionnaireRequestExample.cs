using CarbonTracker.API.Contracts.Questionnaires;
using Swashbuckle.AspNetCore.Filters;
using System.Text.Json;

public sealed class CreateQuestionnaireRequestExample : IExamplesProvider<CreateQuestionnaireRequest>
{
    public CreateQuestionnaireRequest GetExamples() => new()
    {
        Title = "Woon-werk verkeer",
        Status = "draft",
        Definition = JsonDocument.Parse("""
        {
          "title": "Woon-werk verkeer",
          "pages": [
            { "name": "page1", "title": "Woon-werk verkeer",
              "elements": [
                { "type": "rating", "name": "dagen_naar_werk", "rateMin": 0, "rateMax": 7, "isRequired": true },
                { "type": "checkbox", "name": "vervoer",
                  "choices": [
                    {"value":"public_transport","text":"OV"},
                    {"value":"car","text":"Auto"},
                    {"value":"bike","text":"Fiets"}],
                  "validators":[{ "type":"answercount","maxCount":3 }]
                },
                { "type": "text", "name": "reis_tijd_totaal", "inputType": "time" }
              ]
            }
          ]
        }
        """).RootElement
    };
}
