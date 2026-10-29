using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.UnitTests.Markets;

public class PriceSnapshotTests {
  [Fact]
  public void Create_GeneratesVersion7Id() {
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000004"));
    var observedAt = ObservationTime.Create(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc));

    var snapshot = PriceSnapshot.Create(formatId, Money.Create(3.45m), observedAt);

    Assert.Equal(7, snapshot.Id.Value.Version);
  }

  [Fact]
  public void Create_PreservesFormatId() {
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000004"));
    var observedAt = ObservationTime.Create(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc));

    var snapshot = PriceSnapshot.Create(formatId, Money.Create(3.45m), observedAt);

    Assert.Equal(formatId, snapshot.ProductFormatId);
  }

  [Fact]
  public void Create_PreservesPrice() {
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000004"));
    var observedAt = ObservationTime.Create(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc));

    var snapshot = PriceSnapshot.Create(formatId, Money.Create(3.45m), observedAt);

    Assert.Equal(Money.Create(3.45m), snapshot.Price);
  }

  [Fact]
  public void Create_PreservesObservationTime() {
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000004"));
    var observedAt = ObservationTime.Create(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc));

    var snapshot = PriceSnapshot.Create(formatId, Money.Create(3.45m), observedAt);

    Assert.Equal(observedAt, snapshot.ObservedAt);
  }

  [Fact]
  public void Create_GeneratesDistinctIdentifiers() {
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000004"));
    var observedAt = ObservationTime.Create(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc));
    var first = PriceSnapshot.Create(formatId, Money.Create(3.45m), observedAt);

    var second = PriceSnapshot.Create(formatId, Money.Create(3.45m), observedAt);

    Assert.NotEqual(first.Id, second.Id);
  }

  [Fact]
  public void Rehydrate_PreservesSnapshotId() {
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000004"));
    var observedAt = ObservationTime.Create(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc));
    var snapshotId = new PriceSnapshotId(Guid.Parse("00000000-0000-7000-8000-000000000005"));

    var snapshot = PriceSnapshot.Rehydrate(snapshotId, formatId, Money.Create(3.45m), observedAt);

    Assert.Equal(snapshotId, snapshot.Id);
  }

  [Fact]
  public void Rehydrate_PreservesFormatId() {
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000004"));
    var observedAt = ObservationTime.Create(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc));
    var snapshotId = new PriceSnapshotId(Guid.Parse("00000000-0000-7000-8000-000000000005"));

    var snapshot = PriceSnapshot.Rehydrate(snapshotId, formatId, Money.Create(3.45m), observedAt);

    Assert.Equal(formatId, snapshot.ProductFormatId);
  }

  [Fact]
  public void Rehydrate_PreservesPrice() {
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000004"));
    var observedAt = ObservationTime.Create(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc));
    var snapshotId = new PriceSnapshotId(Guid.Parse("00000000-0000-7000-8000-000000000005"));

    var snapshot = PriceSnapshot.Rehydrate(snapshotId, formatId, Money.Create(3.45m), observedAt);

    Assert.Equal(Money.Create(3.45m), snapshot.Price);
  }

  [Fact]
  public void Rehydrate_PreservesObservationTime() {
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000004"));
    var observedAt = ObservationTime.Create(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc));
    var snapshotId = new PriceSnapshotId(Guid.Parse("00000000-0000-7000-8000-000000000005"));

    var snapshot = PriceSnapshot.Rehydrate(snapshotId, formatId, Money.Create(3.45m), observedAt);

    Assert.Equal(observedAt, snapshot.ObservedAt);
  }

  [Fact]
  public void Rehydrate_RestoresRawTimeWithoutValidation() {
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000004"));
    var snapshotId = new PriceSnapshotId(Guid.Parse("00000000-0000-7000-8000-000000000005"));
    var rawTime = new ObservationTime(default);

    var snapshot = PriceSnapshot.Rehydrate(snapshotId, formatId, Money.Create(3.45m), rawTime);

    Assert.Equal(rawTime, snapshot.ObservedAt);
  }

  [Fact]
  public void HasSamePrice_ReturnsTrue_WhenPriceMatches() {
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000004"));
    var observedAt = ObservationTime.Create(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc));
    var snapshot = PriceSnapshot.Create(formatId, Money.Create(3.45m), observedAt);

    bool matches = snapshot.HasSamePrice(Money.Create(3.45m));

    Assert.True(matches);
  }

  [Theory]
  [InlineData(3.44)]
  [InlineData(3.46)]
  public void HasSamePrice_ReturnsFalse_WhenPriceDiffers(decimal amount) {
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000004"));
    var observedAt = ObservationTime.Create(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc));
    var snapshot = PriceSnapshot.Create(formatId, Money.Create(3.45m), observedAt);

    bool matches = snapshot.HasSamePrice(Money.Create(amount));

    Assert.False(matches);
  }

  [Fact]
  public void IsConflictingWith_ReturnsTrue_WhenSameTimeHasDifferentPrice() {
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000004"));
    var observedAt = ObservationTime.Create(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc));
    var snapshot = PriceSnapshot.Create(formatId, Money.Create(3.45m), observedAt);

    bool conflicting = snapshot.IsConflictingWith(Money.Create(3.46m), observedAt);

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
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000004"));
    var observedAt = ObservationTime.Create(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc));
    var snapshot = PriceSnapshot.Create(formatId, Money.Create(3.45m), observedAt);
    var incoming = ObservationTime.Create(observedAt.Value.AddTicks(offset));

    bool conflicting = snapshot.IsConflictingWith(Money.Create(amount), incoming);

    Assert.False(conflicting);
  }

  [Theory]
  [InlineData(-1, true)]
  [InlineData(0, false)]
  [InlineData(1, false)]
  public void IsLaterThan_ComparesStoredTimeToIncomingTime(int offset, bool expected) {
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000004"));
    var observedAt = ObservationTime.Create(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc));
    var snapshot = PriceSnapshot.Create(formatId, Money.Create(3.45m), observedAt);
    var incoming = ObservationTime.Create(observedAt.Value.AddTicks(offset));

    bool later = snapshot.IsLaterThan(incoming);

    Assert.Equal(expected, later);
  }
}