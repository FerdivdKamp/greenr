using CarbonTracker.API.Contracts.Questionnaires;
using CarbonTracker.API.Models;
using DuckDB.NET.Data;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

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
    [SwaggerOperation(
        Summary = "Returns all questionnaires",
        Description = "Returns a list of all questionnaires in the system"
        )
    ]
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
    [SwaggerOperation(
        Summary = "Returns specific questionnaire",
        Description = "Returns the questionnaire with the matching id"
        )
    ]
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
    // --------------------------------------------------------------------
    [HttpGet("{canonicalId:guid}/latest")]
    [SwaggerOperation(
        Summary = "Returns the latest *active* version in a questionnaire family (canonicalId)",
        Description = "Returns the latest version of a canonical."
        )
    ]
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
    [SwaggerOperation(
        Summary = "Publish questionnaire version by id",
        Description = "Activate a version by id. Inactivates the previous active version and sets its replaced_by_id = target."
        )
    ]
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

    [HttpPost("families/{canonicalId:guid}/publish")]
    [SwaggerOperation(Summary = "Publish within a canonical family",
    Description = "Activate a version by targetId, targetVersion, or latest=true. " +
                  "Inactivates the previous active version and sets its replaced_by_id = target.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]        // success (no body)
    [ProducesResponseType(StatusCodes.Status404NotFound)]         // not found
    [ProducesResponseType(StatusCodes.Status400BadRequest)]       // bad input

    public IActionResult PublishByCanonical(Guid canonicalId, [FromBody] PublishByCanonicalRequest body)
    {
        using var conn = CreateConnection();
        using var tx = conn.BeginTransaction();

        // 1) Resolve the target version id within the family
        Guid targetId;

        if (body.TargetId.HasValue)
        {
            using var check = conn.CreateCommand();
            check.Transaction = tx;
            check.CommandText = "SELECT 1 FROM questionnaire WHERE id = ? AND canonical_id = ?";
            check.Parameters.Add(new DuckDBParameter { Value = body.TargetId.Value });
            check.Parameters.Add(new DuckDBParameter { Value = canonicalId });
            var ok = check.ExecuteScalar();
            if (ok is null)
            {
                tx.Rollback();
                return BadRequest("targetId does not belong to the specified canonicalId.");
            }
            targetId = body.TargetId.Value;
        }
        else if (body.TargetVersion.HasValue)
        {
            using var getByVersion = conn.CreateCommand();
            getByVersion.Transaction = tx;
            getByVersion.CommandText = "SELECT id FROM questionnaire WHERE canonical_id = ? AND version = ?";
            getByVersion.Parameters.Add(new DuckDBParameter { Value = canonicalId });
            getByVersion.Parameters.Add(new DuckDBParameter { Value = body.TargetVersion.Value });

            var res = getByVersion.ExecuteScalar()?.ToString();
            if (!Guid.TryParse(res, out targetId))
            {
                tx.Rollback();
                return NotFound("No questionnaire found for that canonicalId + version.");
            }
        }
        else if (body.Latest)
        {
            using var getLatest = conn.CreateCommand();
            getLatest.Transaction = tx;
            getLatest.CommandText = @"
            SELECT id FROM questionnaire
            WHERE canonical_id = ?
            ORDER BY version DESC
            LIMIT 1";
            getLatest.Parameters.Add(new DuckDBParameter { Value = canonicalId });

            var res = getLatest.ExecuteScalar()?.ToString();
            if (!Guid.TryParse(res, out targetId))
            {
                tx.Rollback();
                return NotFound("No versions exist for this canonicalId.");
            }
        }
        else
        {
            tx.Rollback();
            return BadRequest("Provide targetId, targetVersion, or set latest=true.");
        }

        // 2) Inactivate any currently active in this family and link it to the target
        using (var cmdInactive = conn.CreateCommand())
        {
            cmdInactive.Transaction = tx;
            cmdInactive.CommandText = @"
            UPDATE questionnaire
               SET status = 'inactive',
                   updated_at = CURRENT_TIMESTAMP,
                   replaced_by_id = ?
             WHERE canonical_id = ? AND status = 'active' AND id <> ?";
            cmdInactive.Parameters.Add(new DuckDBParameter { Value = targetId });
            cmdInactive.Parameters.Add(new DuckDBParameter { Value = canonicalId });
            cmdInactive.Parameters.Add(new DuckDBParameter { Value = targetId });
            cmdInactive.ExecuteNonQuery();
        }

        // 3) Activate the target (no-op if already active)
        using (var cmdActive = conn.CreateCommand())
        {
            cmdActive.Transaction = tx;
            cmdActive.CommandText = @"
            UPDATE questionnaire
               SET status = 'active', updated_at = CURRENT_TIMESTAMP
             WHERE id = ?";
            cmdActive.Parameters.Add(new DuckDBParameter { Value = targetId });
            var updated = cmdActive.ExecuteNonQuery();
            if (updated == 0)
            {
                tx.Rollback();
                return NotFound("Target questionnaire not found.");
            }
        }

        tx.Commit();
        return NoContent();
    }

    [HttpPost("{questionnaireId:guid}/responses")]
    [SwaggerOperation(Summary = "post response to questionnaire",
    Description = "Post a response, TODO figure out what is expected and match that.")]
    public IActionResult SubmitResponse(Guid questionnaireId, [FromBody] SubmitResponseRequest request)
    {
        // {
           //"userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
           //"answers": { "dagen_naar_werk": 1, "vervoer": ["public_transport"], "reis_tijd_totaal": "00:30"}
        //}

        if (request.Answers.ValueKind != JsonValueKind.Object)
            return BadRequest("`answers` must be a JSON object.");

        using var conn = CreateConnection();
        using var tx = conn.BeginTransaction();

        // 1) Load canonical_id and definition to compute hash (adjust column names if different)
        Guid canonicalId;
        string? definitionJson;

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT canonical_id, definition FROM questionnaire WHERE id = ?";
            cmd.Parameters.Add(new DuckDBParameter { Value = questionnaireId });

            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read())
            {
                tx.Rollback();
                return NotFound($"Questionnaire {questionnaireId} not found.");
            }

            canonicalId = Guid.Parse(rdr.GetValue(0)!.ToString()!);
            definitionJson = rdr.IsDBNull(1) ? null : rdr.GetString(1);
        }

        // 2) Compute a stable hash of the active definition (or empty if not available)
        var definitionHash = ComputeSha256(definitionJson ?? string.Empty);

        // 3) Insert into response
        var responseId = Guid.NewGuid();
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
            INSERT INTO response (id, questionnaire_id, canonical_id, user_id, submitted_at, definition_hash)
            VALUES (?, ?, ?, ?, CURRENT_TIMESTAMP, ?)";
            cmd.Parameters.Add(new DuckDBParameter { Value = responseId });
            cmd.Parameters.Add(new DuckDBParameter { Value = questionnaireId });
            cmd.Parameters.Add(new DuckDBParameter { Value = canonicalId });

            // If your DB column is nullable, pass DBNull when UserId is null.
            // If NOT nullable, consider using Guid.Empty or make the column nullable.
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)request.UserId ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = definitionHash });

            cmd.ExecuteNonQuery();
        }

        // 4) Insert each answer into response_item
        using (var insertItem = conn.CreateCommand())
        {
            insertItem.Transaction = tx;
            insertItem.CommandText = @"
            INSERT INTO response_item (id, response_id, question_id, answer_text, answer_numeric, answer_choice_id)
            VALUES (?, ?, ?, ?, ?, ?)";
            var pId = insertItem.Parameters.Add(new DuckDBParameter());
            var pRespId = insertItem.Parameters.Add(new DuckDBParameter());
            var pQ = insertItem.Parameters.Add(new DuckDBParameter());
            var pText = insertItem.Parameters.Add(new DuckDBParameter());
            var pNum = insertItem.Parameters.Add(new DuckDBParameter());
            var pChoice = insertItem.Parameters.Add(new DuckDBParameter());

            foreach (var prop in request.Answers.EnumerateObject())
            {
                var (text, num, choice) = ToAnswerColumns(prop.Value);

                var questionId = prop.Name;
                var value = prop.Value;

                insertItem.ExecuteNonQuery();
            }
        }

        tx.Commit();

        // 201 Created with { id } payload as your frontend expects
        return Created($"/api/questionnaires/{questionnaireId}/responses/{responseId}", new { id = responseId });

        // === local helpers ===
        static string ComputeSha256(string s)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
            return Convert.ToHexString(bytes); // uppercase hex
        }

        static (string? text, decimal? num, string? choiceId) ToAnswerColumns(JsonElement value)
        {
            // Heuristics:
            // - number -> answer_numeric
            // - string -> answer_text (and maybe treat as choiceId if you later need it)
            // - bool -> "true"/"false" in answer_text
            // - array/object -> store compact JSON in answer_text
            switch (value.ValueKind)
            {
                case JsonValueKind.Number:
                    if (value.TryGetDecimal(out var d)) return (null, d, null);
                    return (value.GetRawText(), null, null); // fallback to text if it’s too big
                case JsonValueKind.String:
                    var s = value.GetString();
                    return (s, null, null);
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return (value.GetBoolean().ToString(), null, null);
                default:
                    // arrays/objects/null -> keep raw JSON so nothing is lost
                    return (value.GetRawText(), null, null);
            }
        }
    }
}
