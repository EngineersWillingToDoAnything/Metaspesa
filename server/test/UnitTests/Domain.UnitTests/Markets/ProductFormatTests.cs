using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.UnitTests.Markets;

public class ProductFormatTests {
  [Fact]
  public void PrepareObservation_RejectsNullObservation() {
    // Act
    static void action() => ProductFormat.PrepareObservation(null!);

    // Assert
    Assert.Throws<ArgumentNullException>(action);
  }

  [Fact]
  public void Create_AssignsObservedPrice() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"), Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));

    // Act
    var format = ProductFormat.Create(prepared, observedOn);

    // Assert
    Assert.True(format.HasPriceObservation(prepared.Price.Amount, observedOn.Value));
  }

  [Fact]
  public void Create_PreservesPreparedQuantity() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));

    // Act
    var format = ProductFormat.Create(prepared, observedOn);

    // Assert
    Assert.Equal(prepared.Quantity, format.Quantity);
  }

  [Fact]
  public void Create_PreservesPreparedImage() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));

    // Act
    var format = ProductFormat.Create(prepared, observedOn);

    // Assert
    Assert.True(format.IsIllustratedBy(prepared.ImageUrl.Value));
  }

  [Fact]
  public void Create_TracksInitialSnapshot() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));

    // Act
    var format = ProductFormat.Create(prepared, observedOn);

    // Assert
    Assert.True(format.HasPendingPriceObservation(prepared.Price.Amount, observedOn.Value));
  }

  [Fact]
  public void Rehydrate_AcceptsMissingPersistedImage() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var original = ProductFormat.Create(prepared, observedOn);

    // Act
    var format = ProductFormat.Rehydrate(original.ToPrimitives() with {
      ImageUrl = null
    });

    // Assert
    Assert.True(format.IsIllustratedBy(null));
  }

  [Fact]
  public void Rehydrate_DoesNotValidatePersistedQuantity() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var original = ProductFormat.Create(prepared, observedOn);
    var legacyQuantity = new Quantity(0, new UnitOfMeasure("legacy"));
    ProductFormatPrimitives persisted = original.ToPrimitives() with {
      Quantity = 0,
      UnitOfMeasure = "legacy"
    };

    // Act
    var format = ProductFormat.Rehydrate(persisted);

    // Assert
    Assert.Equal(legacyQuantity, format.Quantity);
  }

  [Fact]
  public void RecordSnapshot_ReturnsTrue_WhenPriceChanges() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var format = ProductFormat.Create(prepared, observedOn);
    var later = ObservationDate.Create(observedOn.Value.AddDays(1));

    // Act
    bool recorded = format.RecordSnapshot(Money.Create(2.49m), later);

    // Assert
    Assert.True(recorded);
  }

  [Fact]
  public void RecordSnapshot_ReplacesLatestSnapshot_WhenPriceChanges() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var format = ProductFormat.Create(prepared, observedOn);
    var later = ObservationDate.Create(observedOn.Value.AddDays(1));
    var laterPrice = Money.Create(2.49m);

    // Act
    _ = format.RecordSnapshot(laterPrice, later);

    // Assert
    Assert.True(format.HasPriceObservation(laterPrice.Amount, later.Value));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  public void RecordSnapshot_ReturnsFalse_WhenPriceIsUnchanged(
    int daysLater
  ) {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var format = ProductFormat.Create(prepared, observedOn);
    var incoming = ObservationDate.Create(observedOn.Value.AddDays(daysLater));

    // Act
    bool recorded = format.RecordSnapshot(prepared.Price, incoming);

    // Assert
    Assert.False(recorded);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  public void RecordSnapshot_PreservesLatestSnapshot_WhenPriceIsUnchanged(
    int daysLater
  ) {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var format = ProductFormat.Create(prepared, observedOn);
    var incoming = ObservationDate.Create(observedOn.Value.AddDays(daysLater));

    // Act
    _ = format.RecordSnapshot(prepared.Price, incoming);

    // Assert
    Assert.True(format.HasPriceObservation(prepared.Price.Amount, observedOn.Value));
  }

  [Theory]
  [InlineData(1.99)] // Same price
  [InlineData(2.49)] // Different price
  public void RecordSnapshot_RejectsEarlierObservation(decimal price) {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var format = ProductFormat.Create(prepared, observedOn);
    var earlier = ObservationDate.Create(observedOn.Value.AddDays(-1));

    // Act
    void action() => format.RecordSnapshot(Money.Create(price), earlier);

    // Assert
    Assert.Throws<EarlierPriceObservationException>(action);
  }

  [Fact]
  public void RecordSnapshot_RejectsConflictingPriceAtSameTime() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var format = ProductFormat.Create(prepared, observedOn);

    // Act
    void action() => format.RecordSnapshot(Money.Create(2.49m), observedOn);

    // Assert
    Assert.Throws<ConflictingPriceObservationException>(action);
  }

  [Fact]
  public void RecordSnapshot_TracksNewSnapshot() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"), Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var format = ProductFormat.Create(prepared, observedOn);

    var later = ObservationDate.Create(observedOn.Value.AddDays(1));
    var laterPrice = Money.Create(2.49m);

    // Act
    _ = format.RecordSnapshot(laterPrice, later);

    // Assert
    Assert.True(format.HasPendingPriceObservation(laterPrice.Amount, later.Value));
  }

  [Fact]
  public void RecordSnapshot_RetainsEveryPendingSnapshot() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"), Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var format = ProductFormat.Create(prepared, observedOn);

    _ = format.RecordSnapshot(Money.Create(2.49m), ObservationDate.Create(observedOn.Value.AddDays(1)));

    // Act
    _ = format.RecordSnapshot(Money.Create(2.99m), ObservationDate.Create(observedOn.Value.AddDays(2)));

    // Assert
    Assert.True(format.HasPendingPriceObservation(2.49m, observedOn.Value.AddDays(1)));
    Assert.True(format.HasPendingPriceObservation(2.99m, observedOn.Value.AddDays(2)));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  public void RecordSnapshot_DoesNotTrackSnapshot_WhenPriceIsUnchanged(
    int daysLater
  ) {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"), Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var format = ProductFormat.Create(prepared, observedOn);

    var incoming = ObservationDate.Create(observedOn.Value.AddDays(daysLater));

    // Act
    _ = format.RecordSnapshot(prepared.Price, incoming);

    // Assert
    Assert.Equal(1, format.CountPendingPriceObservations());
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(0)]
  public void RecordSnapshot_PreservesPendingSnapshots_WhenObservationIsRejected(
    int daysLater
  ) {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"), Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var format = ProductFormat.Create(prepared, observedOn);

    var incoming = ObservationDate.Create(observedOn.Value.AddDays(daysLater));

    // Act
    _ = Record.Exception(() => format.RecordSnapshot(Money.Create(2.49m), incoming));

    // Assert
    Assert.Equal(1, format.CountPendingPriceObservations());
  }

  [Fact]
  public void Rehydrate_RecordsNoPendingSnapshots() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"), Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var format = ProductFormat.Create(prepared, observedOn);

    // Act
    var restored = ProductFormat.Rehydrate(format.ToPrimitives());

    // Assert
    Assert.Equal(0, restored.CountPendingPriceObservations());
  }

  [Fact]
  public void Rehydrate_PreservesState() {
    // Arrange
    var observedOn = ObservationDate.Create(new DateOnly(2026, 7, 28));
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"), Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var original = ProductFormat.Create(prepared, observedOn);

    // Act
    var restored = ProductFormat.Rehydrate(original.ToPrimitives());

    // Assert
    Assert.Equivalent(original, restored);
  }
}