using Metaspesa.Database.Entities;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Database.UnitTests;

public static class PriceSnapshotDbEntityTest {
  [Fact]
  public static void Entity_PreservesSnapshotId() {
    var entity = new PriceSnapshotDbEntity {
      Id = Guid.Parse("00000000-0000-7000-8000-000000000005"),
      ProductFormatId = Guid.Parse("00000000-0000-7000-8000-000000000007"),
      PriceAmount = 1.99m,
      ObservedOn = new DateOnly(2026, 7, 28),
    };

    PriceSnapshot snapshot = entity.MapToDomain();

    Assert.Equal(new PriceSnapshotId(Guid.Parse("00000000-0000-7000-8000-000000000005")), snapshot.Id);
  }

  [Fact]
  public static void Entity_PreservesPrice() {
    var entity = new PriceSnapshotDbEntity {
      Id = Guid.Parse("00000000-0000-7000-8000-000000000005"),
      ProductFormatId = Guid.Parse("00000000-0000-7000-8000-000000000007"),
      PriceAmount = 1.99m,
      ObservedOn = new DateOnly(2026, 7, 28),
    };

    PriceSnapshot snapshot = entity.MapToDomain();

    Assert.Equal(Money.Create(1.99m), snapshot.Price);
  }

  [Fact]
  public static void Entity_PreservesObservationDate() {
    var entity = new PriceSnapshotDbEntity {
      Id = Guid.Parse("00000000-0000-7000-8000-000000000005"),
      ProductFormatId = Guid.Parse("00000000-0000-7000-8000-000000000007"),
      PriceAmount = 1.99m,
      ObservedOn = new DateOnly(2026, 7, 28),
    };

    PriceSnapshot snapshot = entity.MapToDomain();

    Assert.Equal(new DateOnly(2026, 7, 28), snapshot.ObservedOn.Value);
  }

  [Fact(DisplayName = "Throws specific exception for invalid persisted snapshot id")]
  public static void Entity_ThrowsSpecificException_WhenSnapshotIdIsInvalid() {
    var entity = new PriceSnapshotDbEntity {
      Id = Guid.Empty,
      ProductFormatId = Guid.Parse("00000000-0000-7000-8000-000000000007"),
      PriceAmount = 1.99m,
      ObservedOn = new DateOnly(2026, 7, 28),
    };

    Assert.Throws<InvalidPriceSnapshotIdException>(entity.MapToDomain);
  }

  [Fact(DisplayName = "Restores persisted observation time without running creation validation")]
  public static void Entity_RestoresRawObservationDate() {
    var entity = new PriceSnapshotDbEntity {
      Id = Guid.Parse("00000000-0000-7000-8000-000000000005"),
      ProductFormatId = Guid.Parse("00000000-0000-7000-8000-000000000007"),
      PriceAmount = 1.99m,
      ObservedOn = default,
    };

    PriceSnapshot snapshot = entity.MapToDomain();

    Assert.Equal(default, snapshot.ObservedOn.Value);
  }
}