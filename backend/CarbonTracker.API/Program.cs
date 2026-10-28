using CarbonTracker.API.Services;
using CarbonTracker.API.Services.Interfaces;
using DuckDB.NET.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using System.Data;
using System.Reflection;


var builder = WebApplication.CreateBuilder(args);


// AuthN/Z
builder.Services
  .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(options =>
  {
      var jwtSection = builder.Configuration.GetSection("Jwt");
      options.TokenValidationParameters = new TokenValidationParameters
      {
          ValidateIssuer = true,
          ValidateAudience = true,
          ValidateIssuerSigningKey = true,
          ValidateLifetime = true,
          ClockSkew = TimeSpan.Zero,
          ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
          ValidIssuer = jwtSection["Issuer"] ?? "greenr",
          ValidAudience = jwtSection["Audience"] ?? "greenr",
          IssuerSigningKey = JwtConfiguration.SigningKey(builder.Configuration)
      };
  });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IDbConnection>(_ =>
{
    var path = builder.Configuration.GetConnectionString("DuckDb") ?? "greenr.duckdb";
    var conn = new DuckDBConnection(path);
    conn.Open();
    return conn;
});

builder.Services.AddScoped<IUsersService, UsersService>();
builder.Services.AddSingleton<ITokenService, TokenService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    // Basic doc so UI always has a spec
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CarbonTracker.API",
        Version = "v1",
        Description = "Greenr / CarbonTracker APIs"
    });

    c.EnableAnnotations();

    // Include XML comments if they exist (won't error if missing)
    var xml = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xml);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);

    // Example providers / filters if you use them
    c.ExampleFilters();
    c.DocumentFilter<TagDescriptionsDocumentFilter>();

    // --- Swagger "Authorize" button for Bearer JWT ---
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter: Bearer {your JWT access token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

builder.Services.AddSwaggerExamplesFromAssemblies(Assembly.GetExecutingAssembly());


var app = builder.Build();
// Validate after all configuration providers have been applied, before serving requests.
_ = JwtConfiguration.SigningKey(app.Configuration);

app.UseSwagger();
app.UseSwaggerUI(o =>
{
    // Force the UI to load *this* spec URL explicitly
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "CarbonTracker.API v1");
    o.RoutePrefix = "swagger";
    o.DocumentTitle = "CarbonTracker.API";
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();


public partial class Program { }
