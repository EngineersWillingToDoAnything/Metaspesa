using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.UnitTests.Markets;

public class PriceSnapshotTests {
  [Fact]
  public void Create_GeneratesVersion7Id() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));

    // Act
    var snapshot = PriceSnapshot.Create(Money.Create(3.45m), observedOn);

    // Assert
    Assert.Equal(7, snapshot.Id.Value.Version);
  }

  [Fact]
  public void Create_AssignsObservedPrice() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var price = Money.Create(3.45m);

    // Act
    var snapshot = PriceSnapshot.Create(price, observedOn);

    // Assert
    Assert.True(snapshot.MatchesObservation(price.Amount, observedOn.Value));
  }

  [Fact]
  public void Create_GeneratesDistinctIdentifiers() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var price = Money.Create(3.45m);
    var first = PriceSnapshot.Create(price, observedOn);

    // Act
    var second = PriceSnapshot.Create(price, observedOn);

    // Assert
    Assert.NotEqual(first.Id, second.Id);
  }

  [Fact]
  public void Rehydrate_PreservesState() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var original = PriceSnapshot.Create(Money.Create(3.45m), observedOn);

    // Act
    var restored = PriceSnapshot.Rehydrate(original.ToPrimitives());

    // Assert
    Assert.Equivalent(original, restored);
  }

  [Fact]
  public void Rehydrate_RestoresRawTimeWithoutValidation() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var price = Money.Create(3.45m);
    var original = PriceSnapshot.Create(price, observedOn);
    PriceSnapshotPrimitives persisted = original.ToPrimitives() with { ObservedOn = default };

    // Act
    var snapshot = PriceSnapshot.Rehydrate(persisted);

    // Assert
    Assert.True(snapshot.MatchesObservation(price.Amount, default));
  }
}