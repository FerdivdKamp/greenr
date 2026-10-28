using System.Data;
using System.Security.Claims;
using CarbonTracker.API.Contracts.Items;
using CarbonTracker.API.Models;
using DuckDB.NET.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CarbonTracker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/items")]
[SwaggerTag("Items owned by the signed-in user")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class ItemsController(IDbConnection db) : ControllerBase
{
    private bool TryGetOwner(out Guid owner) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out owner) && owner != Guid.Empty;

    private IDbCommand Command(string sql, params object[] values)
    {
        var command = db.CreateCommand();
        command.CommandText = sql;
        foreach (var value in values)
            command.Parameters.Add(new DuckDBParameter { Value = value });
        return command;
    }

    private static Item ReadItem(IDataRecord reader) => new()
    {
        ItemId = Guid.Parse(reader["item_id"].ToString()!),
        ItemName = reader["item_name"].ToString()!,
        UseCase = reader["use_case"] is DBNull ? "" : reader["use_case"].ToString()!,
        Price = reader["price"] is DBNull ? 0 : Convert.ToDecimal(reader["price"]),
        FootprintKg = reader["footprint_kg"] is DBNull ? 0 : Convert.ToDecimal(reader["footprint_kg"]),
        DateOfPurchase = reader["date_of_purchase"] is DBNull ? null : (DateOnly)reader["date_of_purchase"]
    };

    [HttpGet]
    [SwaggerOperation(Summary = "List your own items")]
    [ProducesResponseType(typeof(List<Item>), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        if (!TryGetOwner(out var owner)) return Unauthorized();
        using var command = Command("SELECT * FROM items WHERE user_id = ? ORDER BY date_of_purchase DESC NULLS LAST, item_id", owner);
        using var reader = command.ExecuteReader();
        var items = new List<Item>();
        while (reader.Read()) items.Add(ReadItem(reader));
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [SwaggerOperation(Summary = "Get one of your own items")]
    [ProducesResponseType(typeof(Item), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetItem(Guid id)
    {
        if (!TryGetOwner(out var owner)) return Unauthorized();
        using var command = Command("SELECT * FROM items WHERE item_id = ? AND user_id = ?", id, owner);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Ok(ReadItem(reader)) : NotFound();
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Create an item owned by you")]
    [ProducesResponseType(typeof(Item), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult AddItem([FromBody] CreateItemRequest request)
    {
        if (!TryGetOwner(out var owner)) return Unauthorized();
        var id = Guid.NewGuid();
        using var command = Command("""
            INSERT INTO items (item_id, user_id, item_name, use_case, price, footprint_kg, date_of_purchase)
            VALUES (?, ?, ?, ?, ?, ?, ?) RETURNING *
            """, id, owner, request.ItemName.Trim(), request.UseCase.Trim(), request.Price!.Value,
            request.FootprintKg!.Value, request.DateOfPurchase ?? (object)DBNull.Value);
        using var reader = command.ExecuteReader();
        reader.Read();
        return CreatedAtAction(nameof(GetItem), new { id }, ReadItem(reader));
    }

    [HttpPut("{id:guid}")]
    [SwaggerOperation(Summary = "Replace one of your own items")]
    [ProducesResponseType(typeof(Item), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult UpdateItem(Guid id, [FromBody] UpdateItemRequest request)
    {
        if (!TryGetOwner(out var owner)) return Unauthorized();
        using var command = Command("""
            UPDATE items SET item_name = ?, use_case = ?, price = ?, footprint_kg = ?, date_of_purchase = ?, updated_at = CURRENT_TIMESTAMP
            WHERE item_id = ? AND user_id = ? RETURNING *
            """, request.ItemName.Trim(), request.UseCase.Trim(), request.Price!.Value,
            request.FootprintKg!.Value, request.DateOfPurchase ?? (object)DBNull.Value, id, owner);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Ok(ReadItem(reader)) : NotFound();
    }

    [HttpDelete("{id:guid}")]
    [SwaggerOperation(Summary = "Delete one of your own items")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DeleteItem(Guid id)
    {
        if (!TryGetOwner(out var owner)) return Unauthorized();
        using var command = Command("DELETE FROM items WHERE item_id = ? AND user_id = ? RETURNING item_id", id, owner);
        using var reader = command.ExecuteReader();
        return reader.Read() ? NoContent() : NotFound();
    }

    [HttpGet("summary")]
    [SwaggerOperation(Summary = "Get your item totals and breakdown by use case",
        Description = "Sums recorded footprint_kg values without amortisation or estimates. Null legacy amounts count as zero; null use cases are grouped with empty use cases.")]
    [ProducesResponseType(typeof(ItemSummary), StatusCodes.Status200OK)]
    public IActionResult Summary()
    {
        if (!TryGetOwner(out var owner)) return Unauthorized();
        using var command = Command("""
            SELECT COALESCE(use_case, '') AS use_case, COUNT(*) AS item_count,
                   COALESCE(SUM(price), 0) AS total_price, COALESCE(SUM(footprint_kg), 0) AS total_footprint_kg
            FROM items WHERE user_id = ? GROUP BY COALESCE(use_case, '') ORDER BY use_case
            """, owner);
        using var reader = command.ExecuteReader();
        var groups = new List<ItemSummaryGroup>();
        while (reader.Read())
            groups.Add(new(reader["use_case"].ToString()!, Convert.ToInt64(reader["item_count"]),
                Convert.ToDecimal(reader["total_price"]), Convert.ToDecimal(reader["total_footprint_kg"])));
        return Ok(new ItemSummary(groups.Sum(g => g.ItemCount), groups.Sum(g => g.TotalPrice),
            groups.Sum(g => g.TotalFootprintKg), groups));
    }
}
