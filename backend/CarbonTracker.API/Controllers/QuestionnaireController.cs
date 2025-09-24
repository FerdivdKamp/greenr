using Microsoft.AspNetCore.Mvc;
using DuckDB.NET.Data;
using CarbonTracker.API.Models;

namespace CarbonTracker.API.Controllers;

[ApiController]
[Route("api/questionnaire")]

public class QuestionnaireController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public QuestionnaireController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private DuckDBConnection CreateConnection()
    {
        var connectionString = _configuration.GetConnectionString("DuckDb");
        var conn = new DuckDBConnection(connectionString);
        conn.Open();
        return conn;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var questionnaires = new List<Questionnaire>();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, title, status, created_at FROM questionnaire";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var questionnaireId = reader["id"].ToString();
            var title = reader["title"]?.ToString() ?? "";
            var status = reader["status"]?.ToString() ?? "";
            var createdAt = Convert.ToDateTime(reader["created_at"]);
            questionnaires.Add(new Questionnaire
            {
                Id = Guid.TryParse(questionnaireId, out var guid) ? guid : Guid.Empty,
                Title = title,
                Status = status,
                CreatedAt = createdAt,
            });
        }
        return Ok(questionnaires);
    }
}
