using Microsoft.AspNetCore.Mvc;
using CarbonTracker.API.Models;
using Swashbuckle.AspNetCore.Annotations;
using System.Data;

namespace CarbonTracker.API.Controllers;

[ApiController]
[Route("api/users")]
[SwaggerTag("Operations related to users")]
public class UsersController : ControllerBase
{
    private readonly IDbConnection _db;  // injected connection

    public UsersController(IDbConnection db)
    {
        _db = db;
    }

    [HttpGet]
    [SwaggerOperation(
        Summary = "Returns all users",
        Description = "Returns the users in the users table."
        )
    ]
    [SwaggerResponse(200, "Users returned", typeof(IEnumerable<User>))]
    public IActionResult Get()
    {
        var users = new List<User>();

        using var cmd = _db.CreateCommand();
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
