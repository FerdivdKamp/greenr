using CarbonTracker.API.Models;
using DuckDB.NET.Data;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CarbonTracker.API.Controllers;

[ApiController]
[Route("api/items")]

public class ItemsController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ItemsController(IConfiguration configuration)
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
    [SwaggerOperation(
        Summary = "Returns all items",
        Description = "Returns the items in the items table. TODO For current user."
        )
    ]
    public IActionResult Get()
    {
        var items = new List<Item>();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT item_id, item_name, use_case, price, footprint_kg, date_of_purchase FROM items";
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var itemId = reader["item_id"].ToString();
            var itemName = reader["item_name"]?.ToString() ?? "";
            var useCase = reader["use_case"]?.ToString() ?? "";
            var price = Convert.ToDecimal(reader["price"]);
            var footprintKg = Convert.ToDecimal(reader["footprint_kg"]);
            DateOnly? dateOfPurchase = reader["date_of_purchase"] == DBNull.Value
                ? null
                : (DateOnly)reader["date_of_purchase"];

            items.Add(new Item
            {
                ItemId = Guid.TryParse(itemId, out var guid) ? guid : Guid.Empty,
                ItemName = itemName,
                UseCase = useCase,
                Price = price,
                FootprintKg = footprintKg,
                DateOfPurchase = dateOfPurchase,
            });
        }

        return Ok(items);
    }

    [HttpPost]
    [SwaggerOperation(
        Summary = "Add item to the items table",
        Description = "Add item to the items table. TODO Make it user aware."
        )
    ]
    public IActionResult AddItem([FromBody] Item item)
    {
        if (item == null)
            return BadRequest();

        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO items (item_id, item_name, use_case, price, footprint_kg, date_of_purchase)
                            VALUES (?, ?, ?, ?, ?, ?)";
        var itemId = item.ItemId == Guid.Empty ? Guid.NewGuid() : item.ItemId;

        cmd.Parameters.Add(new DuckDBParameter { Value = itemId });
        cmd.Parameters.Add(new DuckDBParameter { Value = item.ItemName ?? "" });
        cmd.Parameters.Add(new DuckDBParameter { Value = item.UseCase ?? "" });
        cmd.Parameters.Add(new DuckDBParameter { Value = item.Price });
        cmd.Parameters.Add(new DuckDBParameter { Value = item.FootprintKg });
        cmd.Parameters.Add(new DuckDBParameter { Value = item.DateOfPurchase ?? (object)DBNull.Value });

        var result = cmd.ExecuteNonQuery();
        return result > 0 ? Ok(item) : StatusCode(500, "Insert failed");
    }

    [HttpPut("{id:guid}")]
    [SwaggerOperation(
        Summary = "Update an existing item",
        Description = "Update an existing item in the items table. TODO Make it user aware."
        )
    ]
    public IActionResult UpdateItem(Guid id, [FromBody] Item item)
    {
        if (item == null)
            return BadRequest();

        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"UPDATE items SET item_name = ?, use_case = ?, price = ?, footprint_kg = ?, date_of_purchase = ?
                        WHERE item_id = ?";

        cmd.Parameters.Add(new DuckDBParameter { Value = item.ItemName ?? "" });
        cmd.Parameters.Add(new DuckDBParameter { Value = item.UseCase ?? "" });
        cmd.Parameters.Add(new DuckDBParameter { Value = item.Price });
        cmd.Parameters.Add(new DuckDBParameter { Value = item.FootprintKg });
        cmd.Parameters.Add(new DuckDBParameter { Value = item.DateOfPurchase ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = id });

        var result = cmd.ExecuteNonQuery();
        return result > 0 ? Ok(item) : NotFound();
    }

}
