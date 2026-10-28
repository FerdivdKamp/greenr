using System.ComponentModel.DataAnnotations;

namespace CarbonTracker.API.Contracts.Items;

public abstract class ItemWriteRequest : IValidatableObject
{
    [Required, StringLength(50)]
    public string ItemName { get; init; } = "";

    [Required(AllowEmptyStrings = true), StringLength(20)]
    public string UseCase { get; init; } = "";

    [Required, Range(typeof(decimal), "0", "99999999.99")]
    public decimal? Price { get; init; }

    [Required, Range(typeof(decimal), "0", "9999999.999")]
    public decimal? FootprintKg { get; init; }

    public DateOnly? DateOfPurchase { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Price is decimal price && decimal.Round(price, 2) != price)
            yield return new("Price must have at most two decimal places.", [nameof(Price)]);
        if (FootprintKg is decimal footprint && decimal.Round(footprint, 3) != footprint)
            yield return new("FootprintKg must have at most three decimal places.", [nameof(FootprintKg)]);
        if (DateOfPurchase is DateOnly date && (date == DateOnly.MinValue || date > DateOnly.FromDateTime(DateTime.UtcNow)))
            yield return new("DateOfPurchase must be a valid purchase date on or before today (UTC), or null if unknown.", [nameof(DateOfPurchase)]);
    }
}

public sealed class CreateItemRequest : ItemWriteRequest;
public sealed class UpdateItemRequest : ItemWriteRequest;
