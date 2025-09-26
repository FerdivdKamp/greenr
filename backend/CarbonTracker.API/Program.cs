using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Declare a doc explicitly so UI always has a valid spec
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CarbonTracker.API",
        Version = "v1",
        Description = "Greenr / CarbonTracker APIs"
    });
    c.EnableAnnotations();

    // Include XML comments ONLY if the file exists (prevents errors)
    var xml = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xml);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
    c.ExampleFilters();
    c.DocumentFilter<TagDescriptionsDocumentFilter>();
});

builder.Services.AddSwaggerExamplesFromAssemblies(Assembly.GetExecutingAssembly());


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(o =>
{
    // Force the UI to load *this* spec URL explicitly
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "CarbonTracker.API v1");
    o.RoutePrefix = "swagger";
    o.DocumentTitle = "CarbonTracker.API";
});

app.MapControllers();
app.Run();
