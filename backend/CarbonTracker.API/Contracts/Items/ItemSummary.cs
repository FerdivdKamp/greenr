namespace CarbonTracker.API.Contracts.Items;

public sealed record ItemSummary(long ItemCount, decimal TotalPrice, decimal TotalFootprintKg,
    IReadOnlyList<ItemSummaryGroup> ByUseCase);

public sealed record ItemSummaryGroup(string UseCase, long ItemCount, decimal TotalPrice, decimal TotalFootprintKg);
