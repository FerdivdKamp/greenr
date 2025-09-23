using Microsoft.AspNetCore.Mvc;
using DuckDB.NET.Data;
using CarbonTracker.API.Models;

namespace CarbonTracker.API.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public UsersController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var users = new List<User>();
        var connectionString = _configuration.GetConnectionString("DuckDb");

        using var conn = new DuckDBConnection(connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT user_id, email, first_name FROM users";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            users.Add(new User
            {
                UserId = reader.GetGuid(0),
                Email = reader.GetString(1),
                FirstName = reader.GetString(2)
            });
        }

        return Ok(users);
    }
}
