using CarbonTracker.API.Contracts.Questionnaires;
using CarbonTracker.API.Models;
using DuckDB.NET.Data;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;
using System.Text.Json;

namespace CarbonTracker.API.Controllers;

[ApiController]
[Route("api/questionnaires")]

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

    // --------------------------------------------------------------------
    // GET: /api/questionnaires
    // (List all questionnaires - return full DTOs incl. definition_json)
    // --------------------------------------------------------------------
    [HttpGet]
    //[HttpGet("/api/questionnaire")] // optional: keep old singular route working
    public IActionResult GetAll()
    {
        var list = new List<QuestionnaireDto>();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, canonical_id, version, title, status, created_at, updated_at, definition_json
            FROM questionnaire
            ORDER BY created_at DESC, version DESC";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader["id"]?.ToString();
            var canonicalId = reader["canonical_id"]?.ToString();
            var version = Convert.ToInt32(reader["version"]);
            var title = reader["title"]?.ToString() ?? "";
            var status = reader["status"]?.ToString() ?? "";
            var createdAt = Convert.ToDateTime(reader["created_at"]);
            var updatedAt = Convert.ToDateTime(reader["updated_at"]);
            var definition = reader["definition_json"]?.ToString() ?? "{}";

            list.Add(new QuestionnaireDto(
                Id: Guid.TryParse(id, out var gid) ? gid : Guid.Empty,
                CanonicalId: Guid.TryParse(canonicalId, out var gcanon) ? gcanon : Guid.Empty,
                Version: version,
                Title: title,
                Status: status,
                CreatedAt: createdAt,
                UpdatedAt: updatedAt,
                Definition: definition
            ));
        }

        return Ok(list);
    }

    // --------------------------------------------------------------------
    // GET: /api/questionnaires/{id}
    // --------------------------------------------------------------------
    [HttpGet("{id:guid}")]
    //[HttpGet("/api/questionnaire/{id:guid}")] // optional: old route
    public IActionResult GetById(Guid id)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, canonical_id, version, title, status, created_at, updated_at, definition_json
            FROM questionnaire
            WHERE id = ?";

        cmd.Parameters.Add(new DuckDBParameter { Value = id });

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return NotFound();

        var canonicalId = r["canonical_id"]?.ToString();
        var dto = new QuestionnaireDto(
            Id: id,
            CanonicalId: Guid.TryParse(canonicalId, out var gcanon) ? gcanon : Guid.Empty,
            Version: Convert.ToInt32(r["version"]),
            Title: r["title"]?.ToString() ?? "",
            Status: r["status"]?.ToString() ?? "",
            CreatedAt: Convert.ToDateTime(r["created_at"]),
            UpdatedAt: Convert.ToDateTime(r["updated_at"]),
            Definition: r["definition_json"]?.ToString() ?? "{}"
        );

        return Ok(dto);
    }

    // --------------------------------------------------------------------
    // GET: /api/questionnaires/{canonicalId}/latest
    // Returns the latest *active* version in a questionnaire family
    // --------------------------------------------------------------------
    [HttpGet("{canonicalId:guid}/latest")]
    public IActionResult GetLatest(Guid canonicalId)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, canonical_id, version, title, status, created_at, updated_at, definition_json
            FROM questionnaire
            WHERE canonical_id = ? AND status = 'active'
            ORDER BY version DESC
            LIMIT 1";

        cmd.Parameters.Add(new DuckDBParameter { Value = canonicalId });

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return NotFound();

        var id = r["id"]?.ToString();
        var dto = new QuestionnaireDto(
            Id: Guid.TryParse(id, out var gid) ? gid : Guid.Empty,
            CanonicalId: canonicalId,
            Version: Convert.ToInt32(r["version"]),
            Title: r["title"]?.ToString() ?? "",
            Status: r["status"]?.ToString() ?? "",
            CreatedAt: Convert.ToDateTime(r["created_at"]),
            UpdatedAt: Convert.ToDateTime(r["updated_at"]),
            Definition: r["definition_json"]?.ToString() ?? "{}"
        );

        return Ok(dto);
    }


    [HttpPost]
    [Consumes("application/json")]
    [SwaggerOperation(Summary = "Create questionnaire (new or new version)",
    Description = "Post SurveyJS JSON. Use SupersedesId to bump version in the same canonical.")]
    [SwaggerRequestExample(typeof(CreateQuestionnaireRequest), typeof(CreateQuestionnaireRequestExample))]
    [ProducesResponseType(typeof(QuestionnaireDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Create([FromBody] CreateQuestionnaireRequest dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest("Title is required.");

        // Convert posted definition (SurveyJS JSON) to a string to store
        var definitionJson = dto.Definition.ValueKind switch
        {
            JsonValueKind.String => dto.Definition.GetString() ?? "{}",
            JsonValueKind.Undefined or JsonValueKind.Null => "{}",
            _ => dto.Definition.GetRawText()
        };

        using var conn = CreateConnection();
        using var tx = conn.BeginTransaction();

        // 1) Resolve canonical_id and version
        Guid canonicalId;
        int version;

        if (dto.SupersedesId.HasValue)
        {
            using var cmdPrev = conn.CreateCommand();
            cmdPrev.Transaction = tx;
            cmdPrev.CommandText = "SELECT canonical_id, version FROM questionnaire WHERE id = ?";
            cmdPrev.Parameters.Add(new DuckDBParameter { Value = dto.SupersedesId.Value });

            using var r = cmdPrev.ExecuteReader();
            if (!r.Read())
            {
                tx.Rollback();
                return NotFound($"supersedes_id {dto.SupersedesId} not found.");
            }

            var canonStr = r["canonical_id"]?.ToString();
            if (!Guid.TryParse(canonStr, out canonicalId))
            {
                tx.Rollback();
                return StatusCode(500, "Stored canonical_id is not a valid GUID.");
            }
            var prevVersion = Convert.ToInt32(r["version"]);
            version = prevVersion + 1;
        }
        else
        {
            canonicalId = dto.CanonicalId ?? Guid.NewGuid();
            using var cmdVer = conn.CreateCommand();
            cmdVer.Transaction = tx;
            cmdVer.CommandText = "SELECT COALESCE(MAX(version), 0) FROM questionnaire WHERE canonical_id = ?";
            cmdVer.Parameters.Add(new DuckDBParameter { Value = canonicalId });
            var maxv = Convert.ToInt32(cmdVer.ExecuteScalar() ?? 0);
            version = maxv + 1;
        }

        // 2) Insert new row
        var id = Guid.NewGuid();
        var status = string.IsNullOrWhiteSpace(dto.Status) ? "draft" : dto.Status!.Trim();

        using (var cmdIns = conn.CreateCommand())
        {
            cmdIns.Transaction = tx;
            cmdIns.CommandText = @"
                INSERT INTO questionnaire
                  (id, canonical_id, version, title, definition_json, status, supersedes_id, created_at, updated_at)
                VALUES
                  (?,  ?,            ?,       ?,    ?,              ?,      ?,             CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)";
            cmdIns.Parameters.Add(new DuckDBParameter { Value = id });
            cmdIns.Parameters.Add(new DuckDBParameter { Value = canonicalId });
            cmdIns.Parameters.Add(new DuckDBParameter { Value = version });
            cmdIns.Parameters.Add(new DuckDBParameter { Value = dto.Title });
            cmdIns.Parameters.Add(new DuckDBParameter { Value = definitionJson });
            cmdIns.Parameters.Add(new DuckDBParameter { Value = status });
            cmdIns.Parameters.Add(new DuckDBParameter { Value = dto.SupersedesId ?? (object)DBNull.Value });
            cmdIns.ExecuteNonQuery();
        }

        // 3) If superseding, mark previous row
        if (dto.SupersedesId.HasValue)
        {
            using var cmdUpdPrev = conn.CreateCommand();
            cmdUpdPrev.Transaction = tx;
            cmdUpdPrev.CommandText = @"
                UPDATE questionnaire
                   SET replaced_by_id = ?,
                       status = CASE WHEN status = 'active' THEN 'inactive' ELSE status END,
                       updated_at = CURRENT_TIMESTAMP
                 WHERE id = ?";
            cmdUpdPrev.Parameters.Add(new DuckDBParameter { Value = id });
            cmdUpdPrev.Parameters.Add(new DuckDBParameter { Value = dto.SupersedesId.Value });
            cmdUpdPrev.ExecuteNonQuery();
        }

        tx.Commit();

        var createdDto = new QuestionnaireDto(
            Id: id,
            CanonicalId: canonicalId,
            Version: version,
            Title: dto.Title,
            Status: status,
            CreatedAt: DateTime.UtcNow,     // will be close enough; or re-read row if you want exact DB value
            UpdatedAt: DateTime.UtcNow,
            Definition: definitionJson
        );

        return Created($"/api/questionnaires/{id}", createdDto);
    }

    // --------------------------------------------------------------------
    // POST: /api/questionnaires/{id}/publish
    // Sets this row to 'active' and inactivates prior active in the same family
    // --------------------------------------------------------------------
    [HttpPost("{id:guid}/publish")]
    public IActionResult Publish(Guid id)
    {
        using var conn = CreateConnection();
        using var tx = conn.BeginTransaction();

        // Find canonical of this row
        Guid canonicalId;
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT canonical_id FROM questionnaire WHERE id = ?";
            cmd.Parameters.Add(new DuckDBParameter { Value = id });
            var res = cmd.ExecuteScalar()?.ToString();
            if (!Guid.TryParse(res, out canonicalId))
            {
                tx.Rollback();
                return NotFound("Questionnaire not found.");
            }
        }

        // Inactivate any currently active in this family
        using (var cmdInactive = conn.CreateCommand())
        {
            cmdInactive.Transaction = tx;
            cmdInactive.CommandText = @"
                UPDATE questionnaire
                   SET status = 'inactive', updated_at = CURRENT_TIMESTAMP, replaced_by_id = ?
                 WHERE canonical_id = ? AND status = 'active' AND id <> ?";
            cmdInactive.Parameters.Add(new DuckDBParameter { Value = id });
            cmdInactive.Parameters.Add(new DuckDBParameter { Value = canonicalId });
            cmdInactive.Parameters.Add(new DuckDBParameter { Value = id });
            cmdInactive.ExecuteNonQuery();
        }

        // Activate the target row
        using (var cmdActive = conn.CreateCommand())
        {
            cmdActive.Transaction = tx;
            cmdActive.CommandText = @"
                UPDATE questionnaire
                   SET status = 'active', updated_at = CURRENT_TIMESTAMP
                 WHERE id = ?";
            cmdActive.Parameters.Add(new DuckDBParameter { Value = id });
            var updated = cmdActive.ExecuteNonQuery();
            if (updated == 0)
            {
                tx.Rollback();
                return NotFound("Questionnaire not found.");
            }
        }

        tx.Commit();
        return NoContent();
    }
}
