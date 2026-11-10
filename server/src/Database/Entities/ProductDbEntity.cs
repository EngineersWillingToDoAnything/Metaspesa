using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Database.Entities;

internal class ProductDbEntity {
  public Guid Id { get; set; }
  public string Name { get; set; } = null!;
  public Guid SuperMarketId { get; set; }
  public Guid BrandId { get; set; }

  public MarketDbEntity SuperMarket { get; set; } = null!;
  public BrandDbEntity Brand { get; set; } = null!;
  public ICollection<ProductFormatDbEntity> Formats { get; set; } = [];

  public Product ToAggregate() {
    return Product.Rehydrate(new ProductPrimitives(
      Id,
      Name,
      BrandId,
      SuperMarketId,
      [.. Formats.Select(format => format.ToPrimitives())],
      []
    ));
  }

  public ProductIdentity ToAggregateIdentity() {
    return new ProductIdentity(
      new MarketId(SuperMarketId),
      new BrandId(BrandId),
      new ProductName(Name)
    );
  }
}