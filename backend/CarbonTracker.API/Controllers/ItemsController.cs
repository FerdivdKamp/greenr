using Microsoft.AspNetCore.Mvc;
using DuckDB.NET.Data;
using CarbonTracker.API.Models;

namespace CarbonTracker.API.Controllers;

[ApiController]
[Route("[controller]")]
public class ItemsController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ItemsController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var items = new List<Item>();
        var connectionString = _configuration.GetConnectionString("DuckDb");

        using var conn = new DuckDBConnection(connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT item_id, item_name, use_case, price, footprint_kg, date_of_purchase FROM items";

        using var reader = cmd.ExecuteReader();

        while (reader.Read()) // ✅ This is required before any GetX() call
        {
            var itemId = reader["item_id"].ToString(); // safer than GetGuid
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

}
