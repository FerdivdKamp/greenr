using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public sealed class TagDescriptionsDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument doc, DocumentFilterContext ctx)
    {
        doc.Tags ??= new List<OpenApiTag>();
        Upsert(doc.Tags, "Questionnaire",
            "Create, version, publish, and fetch questionnaires. " +
            "Each version is immutable. Use POST /api/questionnaires/{id}/publish " +
            "to activate a version. GET /api/questionnaires/{canonicalId}/latest " +
            "returns the active version to render in the app.");
    }

    private static void Upsert(IList<OpenApiTag> tags, string name, string description)
    {
        var t = tags.FirstOrDefault(x => x.Name == name);
        if (t == null) tags.Add(new OpenApiTag { Name = name, Description = description });
        else t.Description = description;
    }
}
