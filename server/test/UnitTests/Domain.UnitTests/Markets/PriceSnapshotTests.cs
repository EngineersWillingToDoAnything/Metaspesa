using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.UnitTests.Markets;

public class PriceSnapshotTests {
  [Fact]
  public void Create_GeneratesVersion7Id() {
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));

    var snapshot = PriceSnapshot.Create(Money.Create(3.45m), observedOn);

    Assert.Equal(7, snapshot.Id.Value.Version);
  }

  [Fact]
  public void Create_PreservesPrice() {
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));

    var snapshot = PriceSnapshot.Create(Money.Create(3.45m), observedOn);

    Assert.Equal(Money.Create(3.45m), snapshot.Price);
  }

  [Fact]
  public void Create_PreservesObservationDate() {
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));

    var snapshot = PriceSnapshot.Create(Money.Create(3.45m), observedOn);

    Assert.Equal(observedOn, snapshot.ObservedOn);
  }

  [Fact]
  public void Create_GeneratesDistinctIdentifiers() {
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var first = PriceSnapshot.Create(Money.Create(3.45m), observedOn);

    var second = PriceSnapshot.Create(Money.Create(3.45m), observedOn);

    Assert.NotEqual(first.Id, second.Id);
  }

  [Fact]
  public void Rehydrate_PreservesSnapshotId() {
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var snapshotId = new PriceSnapshotId(Guid.Parse("00000000-0000-7000-8000-000000000005"));

    var snapshot = PriceSnapshot.Rehydrate(snapshotId, Money.Create(3.45m), observedOn);

    Assert.Equal(snapshotId, snapshot.Id);
  }

  [Fact]
  public void Rehydrate_PreservesPrice() {
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var snapshotId = new PriceSnapshotId(Guid.Parse("00000000-0000-7000-8000-000000000005"));

    var snapshot = PriceSnapshot.Rehydrate(snapshotId, Money.Create(3.45m), observedOn);

    Assert.Equal(Money.Create(3.45m), snapshot.Price);
  }

  [Fact]
  public void Rehydrate_PreservesObservationDate() {
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var snapshotId = new PriceSnapshotId(Guid.Parse("00000000-0000-7000-8000-000000000005"));

    var snapshot = PriceSnapshot.Rehydrate(snapshotId, Money.Create(3.45m), observedOn);

    Assert.Equal(observedOn, snapshot.ObservedOn);
  }

  [Fact]
  public void Rehydrate_RestoresRawTimeWithoutValidation() {
    var snapshotId = new PriceSnapshotId(Guid.Parse("00000000-0000-7000-8000-000000000005"));
    var rawTime = new ObservationDate(default);

    var snapshot = PriceSnapshot.Rehydrate(snapshotId, Money.Create(3.45m), rawTime);

    Assert.Equal(rawTime, snapshot.ObservedOn);
  }

  [Fact]
  public void HasSamePrice_ReturnsTrue_WhenPriceMatches() {
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var snapshot = PriceSnapshot.Create(Money.Create(3.45m), observedOn);

    bool matches = snapshot.HasSamePrice(Money.Create(3.45m));

    Assert.True(matches);
  }

  [Theory]
  [InlineData(3.44)]
  [InlineData(3.46)]
  public void HasSamePrice_ReturnsFalse_WhenPriceDiffers(decimal amount) {
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var snapshot = PriceSnapshot.Create(Money.Create(3.45m), observedOn);

    bool matches = snapshot.HasSamePrice(Money.Create(amount));

    Assert.False(matches);
  }

  [Fact]
  public void IsConflictingWith_ReturnsTrue_WhenSameTimeHasDifferentPrice() {
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var snapshot = PriceSnapshot.Create(Money.Create(3.45m), observedOn);

    bool conflicting = snapshot.IsConflictingWith(Money.Create(3.46m), observedOn);

    Assert.True(conflicting);
  }

  [Theory]
  [InlineData(-1, 3.45)]
  [InlineData(-1, 3.46)]
  [InlineData(0, 3.45)]
  [InlineData(1, 3.45)]
  [InlineData(1, 3.46)]
  public void IsConflictingWith_ReturnsFalse_WhenTimeOrPriceDiffers(
    int offset, decimal amount
  ) {
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var snapshot = PriceSnapshot.Create(Money.Create(3.45m), observedOn);
    var incoming = ObservationDate.Create(observedOn.Value.AddDays(offset));

    bool conflicting = snapshot.IsConflictingWith(Money.Create(amount), incoming);

    Assert.False(conflicting);
  }

  [Theory]
  [InlineData(-1, true)]
  [InlineData(0, false)]
  [InlineData(1, false)]
  public void IsLaterThan_ComparesStoredTimeToIncomingTime(int offset, bool expected) {
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var snapshot = PriceSnapshot.Create(Money.Create(3.45m), observedOn);
    var incoming = ObservationDate.Create(observedOn.Value.AddDays(offset));

    bool later = snapshot.IsLaterThan(incoming);

    Assert.Equal(expected, later);
  }
}