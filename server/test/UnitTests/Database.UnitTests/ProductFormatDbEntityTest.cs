using Metaspesa.Database.Entities;
using Metaspesa.Domain.Markets;

namespace Metaspesa.Database.UnitTests;

public static class ProductFormatDbEntityTest {
  [Fact]
  public static void ToPrimitives_MapsFormatAndLatestPriceObservation() {
    // Arrange
    var snapshot = new PriceSnapshotDbEntity {
      Id = Guid.CreateVersion7(),
      PriceAmount = 1.99m,
      ObservedOn = new DateOnly(2026, 7, 28),
    };
    var entity = new ProductFormatDbEntity {
      Id = Guid.CreateVersion7(),
      Quantity = 1000,
      UnitOfMeasure = "ml",
      ImageUrl = "https://example.com/milk.png",
      PriceSnapshots = [snapshot],
    };
    var expected = new ProductFormatPrimitives(
      entity.Id,
      entity.Quantity,
      entity.UnitOfMeasure,
      entity.ImageUrl,
      new PriceSnapshotPrimitives(snapshot.Id, snapshot.PriceAmount, snapshot.ObservedOn),
      []);

    // Act
    ProductFormatPrimitives actual = entity.ToPrimitives();

    // Assert
    Assert.Equivalent(expected, actual, strict: true);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public static void ToPrimitives_SelectsLatestObservationRegardlessOfCollectionOrder(
    bool latestFirst
  ) {
    // Arrange
    var latest = new PriceSnapshotDbEntity {
      Id = Guid.CreateVersion7(),
      PriceAmount = 2.49m,
      ObservedOn = new DateOnly(2026, 7, 29),
    };
    var earlier = new PriceSnapshotDbEntity {
      Id = Guid.CreateVersion7(),
      PriceAmount = 1.99m,
      ObservedOn = latest.ObservedOn.AddDays(-1),
    };
    var entity = new ProductFormatDbEntity {
      Id = Guid.CreateVersion7(),
      Quantity = 1000,
      UnitOfMeasure = "ml",
      ImageUrl = "https://example.com/milk.png",
      PriceSnapshots = latestFirst ? [latest, earlier] : [earlier, latest],
    };
    var expected = new PriceSnapshotPrimitives(
      latest.Id, latest.PriceAmount, latest.ObservedOn);

    // Act
    ProductFormatPrimitives actual = entity.ToPrimitives();

    // Assert
    Assert.Equal(expected, actual.LatestSnapshot);
  }
}