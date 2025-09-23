using CarbonTracker.API.Contracts;
using CarbonTracker.API.Models;

namespace CarbonTracker.API.Mapping;

public static class ItemMapping
{
    public static ItemDto ToDto(this Item x) =>
        new(x.ItemId, x.ItemName, x.UseCase, x.Price, x.FootprintKg, x.DateOfPurchase);

    public static Item ToEntity(this ItemDto x) => new()
    {
        ItemId = x.Id == Guid.Empty ? Guid.NewGuid() : x.Id,
        ItemName = x.Name,
        UseCase = x.UseCase,
        Price = x.Price,
        FootprintKg = x.FootprintKg,
        DateOfPurchase = x.DateOfPurchase
    };
}
