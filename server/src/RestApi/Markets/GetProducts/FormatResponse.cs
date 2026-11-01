using System.Text.Json.Serialization;

namespace Metaspesa.RestApi.Markets.GetProducts;

/// <summary>Purchasable product format with its latest price observation.</summary>
/// <param name="Id">Stable format ID used as the shopping-list item reference.</param>
/// <param name="Quantity">Amount and unit sold in this format.</param>
/// <param name="CurrentPrice">Latest observed price for this format.</param>
/// <param name="ObservedOn">Calendar date of the latest price observation (YYYY-MM-DD).</param>
internal sealed record FormatResponse(
  Guid Id, QuantityResponse Quantity, MoneyResponse CurrentPrice,
  DateOnly ObservedOn) {
  /// <summary>Absolute product image URL when available; otherwise omitted.</summary>
  [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public string? ImageUrl { get; init; }
}