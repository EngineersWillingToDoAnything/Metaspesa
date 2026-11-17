namespace Metaspesa.Domain.Markets;

public readonly record struct ProductIdentity(
  MarketId MarketId,
  BrandId BrandId,
  ProductName Name
) {
  internal static ProductIdentity Create(ProductObservationParams observation) {
    return new ProductIdentity(
      MarketId.Create(observation.MarketId),
      BrandId.Create(observation.BrandId),
      ProductName.Create(observation.ProductName));
  }
};