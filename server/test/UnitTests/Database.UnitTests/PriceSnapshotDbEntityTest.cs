using Metaspesa.Database.Entities;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Database.UnitTests;

public static class PriceSnapshotDbEntityTest {
  [Fact(DisplayName = "Maps persisted price snapshot to immutable domain observation")]
  public static void Entity_MapsToPriceSnapshot() {
    var observedAt = new DateTime(
      2026, 7, 28, 12, 0, 0, DateTimeKind.Unspecified);
    var entity = new PriceSnapshotDbEntity {
      Id = Guid.Parse("00000000-0000-7000-8000-000000000005"),
      ProductFormatId = Guid.Parse("00000000-0000-7000-8000-000000000007"),
      PriceAmount = 1.99m,
      ObservedAt = observedAt,
    };

    PriceSnapshot snapshot = entity.MapToDomain();

    Assert.Equal(new PriceSnapshotId(Guid.Parse("00000000-0000-7000-8000-000000000005")), snapshot.Id);
    Assert.Equal(new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007")), snapshot.ProductFormatId);
    Assert.Equal(new Money(1.99m), snapshot.Price);
    Assert.Equal(DateTimeKind.Utc, snapshot.ObservedAt.Value.Kind);
    Assert.Equal(observedAt, snapshot.ObservedAt.Value);
  }

  [Fact(DisplayName = "Throws specific exception for invalid persisted snapshot id")]
  public static void Entity_ThrowsSpecificException_WhenSnapshotIdIsInvalid() {
    var entity = new PriceSnapshotDbEntity {
      Id = Guid.Empty,
      ProductFormatId = Guid.Parse("00000000-0000-7000-8000-000000000007"),
      PriceAmount = 1.99m,
      ObservedAt = new DateTime(
        2026, 7, 28, 0, 0, 0, DateTimeKind.Unspecified),
    };

    Assert.Throws<InvalidPriceSnapshotIdException>(entity.MapToDomain);
  }

  [Fact(DisplayName = "Restores persisted observation time without running creation validation")]
  public static void Entity_RestoresRawObservationTime() {
    var entity = new PriceSnapshotDbEntity {
      Id = Guid.Parse("00000000-0000-7000-8000-000000000005"),
      ProductFormatId = Guid.Parse("00000000-0000-7000-8000-000000000007"),
      PriceAmount = 1.99m,
      ObservedAt = default,
    };

    PriceSnapshot snapshot = entity.MapToDomain();

    Assert.Equal((DateTime.MinValue.Ticks, DateTimeKind.Utc),
      (snapshot.ObservedAt.Value.Ticks, snapshot.ObservedAt.Value.Kind));
  }
}