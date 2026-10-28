using CarbonTracker.API.Contracts.Questionnaires;
using CarbonTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using DuckDB.NET.Data;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;
using System.Security.Cryptography;
using System.Data;
using System.Text;
using System.Text.Json;

namespace CarbonTracker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/questionnaires")]

public class QuestionnaireController : ControllerBase
{
    private readonly IDbConnection _db;

    public QuestionnaireController(IDbConnection db)
    {
        _db = db;
    }

    private string? ValidatePublishTarget(IDbTransaction tx, Guid id)
    {
        using var command = _db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT definition_json FROM questionnaire WHERE id = ?";
        command.Parameters.Add(new DuckDBParameter { Value = id });
        try
        {
            using var definition = JsonDocument.Parse((string)command.ExecuteScalar()!);
            return QuestionnaireValidation.ValidateDefinition(definition.RootElement);
        }
        catch (JsonException) { return "Stored definition is invalid JSON; create a supported version."; }
    }

    // --------------------------------------------------------------------
    // GET: /api/questionnaires
    // (List all questionnaires - return full DTOs incl. definition_json)
    // --------------------------------------------------------------------
    [HttpGet]
    [SwaggerOperation(
        Summary = "List active questionnaires, or all versions for administrators",
        Description = "Ordinary users see active definitions only. Administrators also see drafts and inactive versions."
        )
    ]
    public IActionResult GetAll()
    {
        var list = new List<QuestionnaireDto>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT id, canonical_id, version, title, status, created_at, updated_at, definition_json
            FROM questionnaire
            WHERE (? OR status = 'active')
            ORDER BY created_at DESC, version DESC";

        cmd.Parameters.Add(new DuckDBParameter { Value = User.IsInRole("QuestionnaireAdmin") });
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
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT id, canonical_id, version, title, status, created_at, updated_at, definition_json
            FROM questionnaire
            WHERE id = ? AND (? OR status = 'active')";

        cmd.Parameters.Add(new DuckDBParameter { Value = id });
        cmd.Parameters.Add(new DuckDBParameter { Value = User.IsInRole("QuestionnaireAdmin") });

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
        using var cmd = _db.CreateCommand();
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
    [Authorize(Roles = "QuestionnaireAdmin")]
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

        if (dto.Status is not (null or "draft"))
            return BadRequest("New versions must be draft; use a publish endpoint to activate them.");
        if (dto.CanonicalId == Guid.Empty || dto.SupersedesId == Guid.Empty)
            return BadRequest("Family and supersedes IDs must not be empty.");
        var definitionError = QuestionnaireValidation.ValidateDefinition(dto.Definition);
        if (definitionError is not null) return BadRequest(definitionError);
        var definitionJson = dto.Definition.GetRawText();

        using var tx = _db.BeginTransaction();

        // 1) Resolve canonical_id and version
        Guid canonicalId;
        int version;

        if (dto.SupersedesId.HasValue)
        {
            using var cmdPrev = _db.CreateCommand();
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
            if (dto.CanonicalId.HasValue && dto.CanonicalId != canonicalId)
                return BadRequest("canonicalId does not match supersedesId.");
            r.Close();
            using var max = _db.CreateCommand();
            max.Transaction = tx;
            max.CommandText = "SELECT MAX(version) FROM questionnaire WHERE canonical_id = ?";
            max.Parameters.Add(new DuckDBParameter { Value = canonicalId });
            version = Convert.ToInt32(max.ExecuteScalar()) + 1;
        }
        else
        {
            canonicalId = dto.CanonicalId ?? Guid.NewGuid();
            using var cmdVer = _db.CreateCommand();
            cmdVer.Transaction = tx;
            cmdVer.CommandText = "SELECT COALESCE(MAX(version), 0) FROM questionnaire WHERE canonical_id = ?";
            cmdVer.Parameters.Add(new DuckDBParameter { Value = canonicalId });
            var maxv = Convert.ToInt32(cmdVer.ExecuteScalar() ?? 0);
            version = maxv + 1;
        }

        // 2) Insert new row
        var id = Guid.NewGuid();
        var status = "draft";

        using (var cmdIns = _db.CreateCommand())
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
    [Authorize(Roles = "QuestionnaireAdmin")]
    [SwaggerOperation(
        Summary = "Publish questionnaire version by id",
        Description = "Activate a version by id. Inactivates the previous active version and sets its replaced_by_id = target."
        )
    ]
    public IActionResult Publish(Guid id)
    {
        using var tx = _db.BeginTransaction();

        // Find canonical of this row
        Guid canonicalId;
        using (var cmd = _db.CreateCommand())
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

        var definitionError = ValidatePublishTarget(tx, id);
        if (definitionError is not null) return BadRequest(definitionError);

        // Inactivate any currently active in this family
        using (var cmdInactive = _db.CreateCommand())
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
        using (var cmdActive = _db.CreateCommand())
        {
            cmdActive.Transaction = tx;
            cmdActive.CommandText = @"
                UPDATE questionnaire
                   SET status = 'active', updated_at = CURRENT_TIMESTAMP, replaced_by_id = NULL
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
    [Authorize(Roles = "QuestionnaireAdmin")]
    [SwaggerOperation(Summary = "Publish within a canonical family",
    Description = "Activate a version by targetId, targetVersion, or latest=true. " +
                  "Inactivates the previous active version and sets its replaced_by_id = target.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]        // success (no body)
    [ProducesResponseType(StatusCodes.Status404NotFound)]         // not found
    [ProducesResponseType(StatusCodes.Status400BadRequest)]       // bad input

    public IActionResult PublishByCanonical(Guid canonicalId, [FromBody] PublishByCanonicalRequest body)
    {
        using var tx = _db.BeginTransaction();

        if ((body.TargetId.HasValue ? 1 : 0) + (body.TargetVersion.HasValue ? 1 : 0) + (body.Latest ? 1 : 0) != 1
            || body.TargetId == Guid.Empty || body.TargetVersion <= 0)
            return BadRequest("Provide exactly one of targetId, positive targetVersion, or latest=true.");

        // 1) Resolve the target version id within the family
        Guid targetId;

        if (body.TargetId.HasValue)
        {
            using var check = _db.CreateCommand();
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
            using var getByVersion = _db.CreateCommand();
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
            using var getLatest = _db.CreateCommand();
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

        var definitionError = ValidatePublishTarget(tx, targetId);
        if (definitionError is not null) return BadRequest(definitionError);

        // 2) Inactivate any currently active in this family and link it to the target
        using (var cmdInactive = _db.CreateCommand())
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
        using (var cmdActive = _db.CreateCommand())
        {
            cmdActive.Transaction = tx;
            cmdActive.CommandText = @"
            UPDATE questionnaire
               SET status = 'active', updated_at = CURRENT_TIMESTAMP, replaced_by_id = NULL
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
    Description = "Submit validated answers to an active version as the signed-in user.")]
    public IActionResult SubmitResponse(Guid questionnaireId, [FromBody] SubmitResponseRequest request)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var owner) || owner == Guid.Empty)
            return Unauthorized();

        if (request.Answers.ValueKind != JsonValueKind.Object)
            return BadRequest("`answers` must be a JSON object.");

        using var tx = _db.BeginTransaction();

        // 1) Load canonical_id and definition to compute hash (adjust column names if different)
        Guid canonicalId;
        string? definitionJson;

        using (var cmd = _db.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT canonical_id, definition_json FROM questionnaire WHERE id = ? AND status = 'active'";
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

        JsonDocument definition;
        try { definition = JsonDocument.Parse(definitionJson!); }
        catch (JsonException) { return BadRequest("Stored definition is invalid JSON; an administrator must publish a supported version."); }
        using var definitionScope = definition;
        var answerError = QuestionnaireValidation.ValidateAnswers(definition.RootElement, request.Answers);
        if (answerError is not null) return BadRequest(answerError);

        // 2) Compute a stable hash of the active definition (or empty if not available)
        var definitionHash = ComputeSha256(definitionJson ?? string.Empty);

        // 3) Insert into response
        var responseId = Guid.NewGuid();
        var answersRaw = request.Answers.GetRawText();

        using (var cmd = _db.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
            INSERT INTO response (id, questionnaire_id, canonical_id, user_id, submitted_at, definition_hash, answers_json)
            VALUES (?, ?, ?, ?, CURRENT_TIMESTAMP, ?,?)";
            cmd.Parameters.Add(new DuckDBParameter { Value = responseId });
            cmd.Parameters.Add(new DuckDBParameter { Value = questionnaireId });
            cmd.Parameters.Add(new DuckDBParameter { Value = canonicalId });
            cmd.Parameters.Add(new DuckDBParameter { Value = owner });

            cmd.Parameters.Add(new DuckDBParameter { Value = definitionHash });
            cmd.Parameters.Add(new DuckDBParameter { Value = answersRaw });

            cmd.ExecuteNonQuery();
        }

        // 4) Insert each answer into response_item
        using (var insertItem = _db.CreateCommand())
        {
            insertItem.Transaction = tx;
            insertItem.CommandText = @"
            INSERT INTO response_item (id, response_id, question_id, answer_text, answer_numeric, answer_choice_id)
            VALUES (?, ?, ?, ?, ?, ?)";

            foreach (var prop in request.Answers.EnumerateObject())
            {
                var (text, num, choice) = ToAnswerColumns(prop.Value);

                insertItem.Parameters.Clear(); // clear previous loop values

                insertItem.Parameters.Add(new DuckDBParameter { Value = Guid.NewGuid() }); // id
                insertItem.Parameters.Add(new DuckDBParameter { Value = responseId });     // response_id
                insertItem.Parameters.Add(new DuckDBParameter { Value = prop.Name });      // question_id
                insertItem.Parameters.Add(new DuckDBParameter { Value = (object?)text ?? DBNull.Value });
                insertItem.Parameters.Add(new DuckDBParameter { Value = (object?)num ?? DBNull.Value });
                insertItem.Parameters.Add(new DuckDBParameter { Value = (object?)choice ?? DBNull.Value });

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
                    // response_item has DECIMAL(18,4); keep other numbers in text
                    // so projection cannot overflow or silently round the raw answer.
                    if (value.TryGetDecimal(out var d) && Math.Abs(d) <= 99999999999999.9999m
                        && decimal.Round(d, 4) == d) return (null, d, null);
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
