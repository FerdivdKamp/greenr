using CarbonTracker.API.Services;
using DuckDB.NET.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using System.Data;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);


// Read JWT config (Configuration refers to appsettings.json and env vars)
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
var jwtIssuer = jwtSection["Issuer"] ?? "greenr";
var jwtAudience = jwtSection["Audience"] ?? "greenr";


var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));


// AuthN/Z
builder.Services
  .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(options =>
  {
      options.TokenValidationParameters = new TokenValidationParameters
      {
          ValidateIssuer = true,
          ValidateAudience = true,
          ValidateIssuerSigningKey = true,
          ValidIssuer = jwtIssuer,
          ValidAudience = jwtAudience,
          IssuerSigningKey = signingKey
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
