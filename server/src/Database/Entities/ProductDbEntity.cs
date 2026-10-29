using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Database.Entities;

internal class ProductDbEntity {
  public Guid Id { get; set; }
  public string Name { get; set; } = null!;
  public Guid SuperMarketId { get; set; }
  public Guid BrandId { get; set; }

  public SuperMarketDbEntity SuperMarket { get; set; } = null!;
  public ProductBrandDbEntity Brand { get; set; } = null!;
  public ICollection<ProductFormatDbEntity> Formats { get; set; } = [];

  public Product MapToDomain() => Product.Rehydrate(
    new ProductId(Id),
    new ProductName(Name),
    new BrandId(BrandId),
    new MarketId(SuperMarketId),
    Formats.Select(format => ProductFormat.Rehydrate(
      new ProductFormatId(format.Id),
      new Quantity(
        format.Quantity,
        new UnitOfMeasure(format.UnitOfMeasure)),
      string.IsNullOrWhiteSpace(format.ImageUrl)
        ? null
        : new ImageUrl(new Uri(format.ImageUrl, UriKind.Absolute)),
      format.PriceSnapshots
        .OrderByDescending(snapshot => snapshot.ObservedAt)
        .ThenByDescending(snapshot => snapshot.Id)
        .First().MapToDomain())));
}