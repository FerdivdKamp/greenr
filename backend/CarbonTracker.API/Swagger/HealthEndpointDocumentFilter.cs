using CarbonTracker.API.Services;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CarbonTracker.API.Swagger;

// MapHealthChecks is middleware, so document its response explicitly.
public sealed class HealthEndpointDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        static OpenApiResponse Response(string description, string status) => new()
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["status"] = new() { Type = "string", Enum = new List<IOpenApiAny> { new OpenApiString(status) } },
                            ["database"] = new() { Type = "string", Enum = new List<IOpenApiAny> { new OpenApiString(status) } }
                        },
                        Required = new HashSet<string> { "status", "database" }
                    }
                }
            }
        };

        document.Paths[DatabaseHealthCheck.EndpointPath] = new OpenApiPathItem
        {
            Operations = new Dictionary<OperationType, OpenApiOperation>
            {
                [OperationType.Get] = new()
                {
                    Tags = new List<OpenApiTag> { new() { Name = "Internal" } },
                    Summary = "Check API and database health",
                    Description = "Anonymous readiness check. The Internal tag groups operational endpoints; it does not restrict access.",
                    Responses = new OpenApiResponses
                    {
                        ["200"] = Response("The database health check passed.", "healthy"),
                        ["503"] = Response("The database health check failed.", "unhealthy")
                    }
                }
            }
        };
    }
}
