using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CarbonTracker.API.Swagger;

public class ApiErrorFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { StatusCode: >= 400 } result && result.Value is string message)
            context.Result = NewProblem(result.StatusCode!.Value, message);
    }
    private static ObjectResult NewProblem(int status, string message) => new(new ProblemDetails { Status = status, Title = message }) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    public void OnResultExecuted(ResultExecutedContext context) { }
}

public class ApiOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        var authorized = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();
        if (authorized)
        {
            operation.Security = new List<OpenApiSecurityRequirement> { new() { [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>() } };
            AddProblem(operation, context, "401");
            if (metadata.OfType<IAuthorizeData>().Any(a => !string.IsNullOrEmpty(a.Roles))) AddProblem(operation, context, "403");
        }
        if (context.ApiDescription.ParameterDescriptions.Any(p => p.Source?.Id == "Body")) AddProblem(operation, context, "400");
        foreach (var code in new[] { "401", "403", "404", "409" }) if (operation.Responses.ContainsKey(code)) AddProblem(operation, context, code);
    }
    private static void AddProblem(OpenApiOperation operation, OperationFilterContext context, string code)
    {
        if (!operation.Responses.TryGetValue(code, out var response)) operation.Responses[code] = response = new OpenApiResponse { Description = "Request failed" };
        response.Content["application/problem+json"] = new OpenApiMediaType { Schema = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository) };
    }
}

