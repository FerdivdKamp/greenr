namespace CarbonTracker.API.Models;

public class Item
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public string UseCase { get; set; } = "";
    public decimal Price { get; set; }
    public decimal FootprintKg { get; set; }
    public DateTime DateOfPurchase { get; set; }

}
