using Metaspesa.Database.Entities;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Database.UnitTests;

public static class ProductDbEntityTest {
  [Fact(DisplayName = "Maps product and formats without price snapshots")]
  public static void Entity_MapsToProductAggregate_WithoutPriceSnapshots() {
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
    Assert.Equal(new BrandName("Brand"), product.Brand);
    Assert.Equal(new MarketId(Guid.Parse("00000000-0000-7000-8000-000000000002")), product.MarketId);
    ProductFormat format = Assert.Single(product.Formats);
    Assert.Equal(new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007")), format.Id);
    Assert.Equal(1m, format.Quantity.Amount);
    Assert.DoesNotContain(
      typeof(ProductFormat).GetProperties(),
      property => property.PropertyType == typeof(PriceSnapshot));
  }

  [Fact(DisplayName = "Throws specific exception for invalid persisted product id")]
  public static void Entity_ThrowsSpecificException_WhenProductIdIsInvalid() {
    ProductDbEntity entity = CreateEntity();
    entity.Id = Guid.Empty;

    Assert.Throws<InvalidProductIdException>(entity.MapToDomain);
  }

  private static ProductDbEntity CreateEntity() => new() {
    Id = Guid.Parse("00000000-0000-7000-8000-000000000003"),
    Name = "Milk",
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