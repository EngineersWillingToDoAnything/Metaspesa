using Metaspesa.Database.Entities;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Database.UnitTests;

public static class ProductDbEntityTest {
  [Fact(DisplayName = "Maps product and formats with latest price snapshots")]
  public static void Entity_MapsToProductAggregate_WithLatestPriceSnapshots() {
    ProductDbEntity entity = CreateEntity();
    entity.Formats.Single().PriceSnapshots.Add(new PriceSnapshotDbEntity {
      Id = Guid.Parse("00000000-0000-7000-8000-000000000008"),
      ProductFormatId = Guid.Parse("00000000-0000-7000-8000-000000000007"),
      PriceAmount = 1.99m,
      ObservedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
    });

    Product product = entity.MapToDomain();

    Assert.Equal(new ProductId(Guid.Parse("00000000-0000-7000-8000-000000000003")), product.Id);
    Assert.Equal(new ProductName("Milk"), product.Name);
    Assert.Equal(new BrandId(entity.BrandId), product.BrandId);
    Assert.Equal(new MarketId(Guid.Parse("00000000-0000-7000-8000-000000000002")), product.MarketId);
    ProductFormat format = Assert.Single(product.Formats);
    Assert.Equal(new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007")), format.Id);
    Assert.Equal(1m, format.Quantity.Amount);
    Assert.Equal(1.99m, format.LatestSnapshot.Price.Amount);
    Assert.Empty(product.PendingSnapshots);
  }

  [Fact(DisplayName = "Throws specific exception for invalid persisted product id")]
  public static void Entity_ThrowsSpecificException_WhenProductIdIsInvalid() {
    ProductDbEntity entity = CreateEntity();
    entity.Id = Guid.Empty;

    Assert.Throws<InvalidProductIdException>(entity.MapToDomain);
  }

  [Fact(DisplayName = "Maps the latest persisted snapshot without registering changes")]
  public static void Entity_MapsLatestSnapshot_WhenHistoryIsPresent() {
    ProductDbEntity entity = CreateEntity();
    ProductFormatDbEntity format = entity.Formats.Single();
    format.PriceSnapshots = [
      new PriceSnapshotDbEntity {
        Id = Guid.Parse("00000000-0000-7000-8000-000000000008"),
        ProductFormatId = format.Id,
        PriceAmount = 2.49m,
        ObservedAt = new DateTime(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc),
      },
      new PriceSnapshotDbEntity {
        Id = Guid.Parse("00000000-0000-7000-8000-000000000009"),
        ProductFormatId = format.Id,
        PriceAmount = 1.99m,
        ObservedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      },
    ];

    Product product = entity.MapToDomain();

    Assert.Equal(2.49m, Assert.Single(product.Formats).LatestSnapshot.Price.Amount);
  }

  private static ProductDbEntity CreateEntity() => new() {
    Id = Guid.Parse("00000000-0000-7000-8000-000000000003"),
    Name = "Milk",
    BrandId = Guid.Parse("00000000-0000-7000-8000-000000000004"),
    SuperMarketId = Guid.Parse("00000000-0000-7000-8000-000000000002"),
    Brand = new ProductBrandDbEntity { Name = "Brand" },
    Formats = [
      new ProductFormatDbEntity {
        Id = Guid.Parse("00000000-0000-7000-8000-000000000007"),
        Quantity = 1m,
        ImageUrl = "https://example.com/milk.png",
        UnitOfMeasure = "ml",
      },
    ],
  };
}