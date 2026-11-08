using Metaspesa.Database.Entities;
using Metaspesa.Domain.Markets;

namespace Metaspesa.Database.UnitTests;

public static class ProductDbEntityTest {
  [Fact]
  public static void ToAggregate_MapsProductAndFormats() {
    // Arrange
    var expected = Product.Create(new ProductObservationParams(
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      "Milk",
      new DateOnly(2026, 7, 28),
      [
        new(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
        new(2, "l", 3.49m, new Uri("https://example.com/large.png"))
      ]));
    ProductPrimitives persisted = expected.ToPrimitives();
    var entity = new ProductDbEntity {
      Id = persisted.Id,
      Name = persisted.Name,
      BrandId = persisted.BrandId,
      SuperMarketId = persisted.MarketId,
      Formats = [.. persisted.Formats.Select(format => new ProductFormatDbEntity {
        Id = format.Id,
        ProductId = persisted.Id,
        Quantity = format.Quantity,
        UnitOfMeasure = format.UnitOfMeasure,
        ImageUrl = format.ImageUrl!,
        PriceSnapshots = [new PriceSnapshotDbEntity {
          Id = format.LatestSnapshot.Id,
          ProductFormatId = format.Id,
          PriceAmount = format.LatestSnapshot.Price,
          ObservedOn = format.LatestSnapshot.ObservedOn,
        }],
      })],
    };

    // Act
    Product actual = entity.ToAggregate();

    // Assert
    Assert.Equivalent(expected, actual, strict: true);
  }

  [Fact]
  public static void ToAggregateIdentity_MapsTheStoredProductIdentity() {
    // Arrange
    var entity = new ProductDbEntity {
      Id = Guid.CreateVersion7(),
      Name = "Milk",
      BrandId = Guid.CreateVersion7(),
      SuperMarketId = Guid.CreateVersion7(),
    };
    var expected = new ProductIdentity(
      new MarketId(entity.SuperMarketId),
      new BrandId(entity.BrandId),
      new ProductName(entity.Name));

    // Act
    ProductIdentity actual = entity.ToAggregateIdentity();

    // Assert
    Assert.Equal(expected, actual);
  }
}