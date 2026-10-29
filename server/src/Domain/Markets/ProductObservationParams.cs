namespace Metaspesa.Domain.Markets;

public sealed record ProductObservationParams(
  Guid MarketId,
  Guid BrandId,
  string ProductName,
  DateTime ObservedAt,
  IReadOnlyCollection<ProductFormatObservationParams> Formats
);

public sealed record ProductFormatObservationParams(
  decimal Quantity,
  string UnitOfMeasure,
  decimal Price,
  Uri ImageUrl
);