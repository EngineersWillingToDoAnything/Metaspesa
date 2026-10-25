namespace Metaspesa.RestApi.Markets.AddSnapshot;

/// <summary>Batch of market product observations.</summary>
/// <param name="Items">Non-empty array of valid product observations.</param>
internal sealed record SnapshotRequest(IReadOnlyList<SnapshotItem?>? Items);

/// <summary>One observed product format in a market snapshot.</summary>
/// <param name="Name">Required nonblank product name.</param>
/// <param name="Price">Observed non-negative price amount.</param>
/// <param name="Quantity">Positive amount; converted quantity must fit numeric(10,3) exactly.</param>
/// <param name="UnitOfMeasure">Supported unit or alias, converted to g, ml, unit, wash, or dose.</param>
/// <param name="BrandName">Required nonblank brand name.</param>
/// <param name="ImageUrl">Optional absolute product image URL.</param>
internal sealed record SnapshotItem(
  string? Name,
  decimal Price,
  decimal Quantity,
  string? UnitOfMeasure,
  string? BrandName,
  string? ImageUrl);