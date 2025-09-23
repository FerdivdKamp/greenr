namespace CarbonTracker.API.Contracts
{
    public record ItemDto(
    Guid Id,
    string Name,
    string UseCase,
    decimal Price,
    decimal FootprintKg,
    DateOnly? DateOfPurchase
);
}
