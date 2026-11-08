using Metaspesa.Database.Entities;
using Metaspesa.Domain.Markets;

namespace Metaspesa.Database.UnitTests;

public static class PriceSnapshotDbEntityTest {
  [Fact]
  public static void ToPrimitives_MapsStoredPriceObservation() {
    // Arrange
    var entity = new PriceSnapshotDbEntity {
      Id = Guid.CreateVersion7(), ProductFormatId = Guid.CreateVersion7(),
      PriceAmount = 1.99m, ObservedOn = new DateOnly(2026, 7, 28),
    };
    var expected = new PriceSnapshotPrimitives(
      entity.Id, entity.PriceAmount, entity.ObservedOn);

    // Act
    PriceSnapshotPrimitives actual = entity.ToPrimitives();

    // Assert
    Assert.Equal(expected, actual);
  }
}