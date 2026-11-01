namespace Metaspesa.Application.Abstractions.Markets;

public sealed record CatalogProduct(
  Guid Id,
  string Name,
  string Brand,
  MarketSummary Market,
  IReadOnlyCollection<CatalogFormat> Formats);

public sealed record CatalogFormat(
  Guid Id,
  decimal Quantity,
  string Unit,
  decimal Price,
  string Currency,
  Uri? ImageUrl,
  DateOnly ObservedOn);