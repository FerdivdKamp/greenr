namespace CarbonTracker.API.Models;

public class User
{
    public Guid UserId { get; set; }
    public required string Email { get; set; }
    public required string UserName { get; set; }
}
