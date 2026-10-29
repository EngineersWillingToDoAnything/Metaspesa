using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.UnitTests.Markets;

public class ProductFormatTests {
  [Fact]
  public void PrepareObservation_RejectsNullObservation() {
    static void action() => ProductFormat.PrepareObservation(null!);

    Assert.Throws<ArgumentNullException>(action);
  }

  [Fact]
  public void Create_GeneratesVersion7FormatId() {
    var observedAt = ObservationTime.Create(DateTime.UtcNow);
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));

    var format = ProductFormat.Create(prepared, observedAt);

    Assert.Equal(7, format.Id.Value.Version);
  }

  [Fact]
  public void Create_PreservesPreparedQuantity() {
    var observedAt = ObservationTime.Create(DateTime.UtcNow);
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));

    var format = ProductFormat.Create(prepared, observedAt);

    Assert.Equal(prepared.Quantity, format.Quantity);
  }

  [Fact]
  public void Create_PreservesPreparedImage() {
    var observedAt = ObservationTime.Create(DateTime.UtcNow);
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));

    var format = ProductFormat.Create(prepared, observedAt);

    Assert.Equal(prepared.ImageUrl, format.ImageUrl);
  }

  [Fact]
  public void Create_CreatesInitialSnapshotForFormat() {
    var observedAt = ObservationTime.Create(DateTime.UtcNow);
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));

    var format = ProductFormat.Create(prepared, observedAt);

    Assert.Equal(format.Id, format.LatestSnapshot.ProductFormatId);
  }

  [Fact]
  public void Rehydrate_PreservesFormatId() {
    var observedAt = ObservationTime.Create(DateTime.UtcNow);
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var original = ProductFormat.Create(prepared, observedAt);

    var format = ProductFormat.Rehydrate(
      original.Id, original.Quantity, original.ImageUrl, original.LatestSnapshot);

    Assert.Equal(original.Id, format.Id);
  }

  [Fact]
  public void Rehydrate_PreservesQuantity() {
    var observedAt = ObservationTime.Create(DateTime.UtcNow);
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var original = ProductFormat.Create(prepared, observedAt);

    var format = ProductFormat.Rehydrate(
      original.Id, original.Quantity, original.ImageUrl, original.LatestSnapshot);

    Assert.Equal(original.Quantity, format.Quantity);
  }

  [Fact]
  public void Rehydrate_PreservesImage() {
    var observedAt = ObservationTime.Create(DateTime.UtcNow);
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var original = ProductFormat.Create(prepared, observedAt);

    var format = ProductFormat.Rehydrate(
      original.Id, original.Quantity, original.ImageUrl, original.LatestSnapshot);

    Assert.Equal(original.ImageUrl, format.ImageUrl);
  }

  [Fact]
  public void Rehydrate_PreservesLatestSnapshot() {
    var observedAt = ObservationTime.Create(DateTime.UtcNow);
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var original = ProductFormat.Create(prepared, observedAt);

    var format = ProductFormat.Rehydrate(
      original.Id, original.Quantity, original.ImageUrl, original.LatestSnapshot);

    Assert.Same(original.LatestSnapshot, format.LatestSnapshot);
  }

  [Fact]
  public void Rehydrate_AcceptsMissingPersistedImage() {
    var observedAt = ObservationTime.Create(DateTime.UtcNow);
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var original = ProductFormat.Create(prepared, observedAt);

    var format = ProductFormat.Rehydrate(
      original.Id, original.Quantity, null, original.LatestSnapshot);

    Assert.Null(format.ImageUrl);
  }

  [Fact]
  public void Rehydrate_DoesNotValidatePersistedQuantity() {
    var observedAt = ObservationTime.Create(DateTime.UtcNow);
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var original = ProductFormat.Create(prepared, observedAt);
    var legacyQuantity = new Quantity(0, new UnitOfMeasure("legacy"));

    var format = ProductFormat.Rehydrate(
      original.Id, legacyQuantity, original.ImageUrl, original.LatestSnapshot);

    Assert.Equal(legacyQuantity, format.Quantity);
  }

  [Fact]
  public void RecordSnapshot_ReturnsTrue_WhenPriceChanges() {
    var observedAt = ObservationTime.Create(DateTime.UtcNow);
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var format = ProductFormat.Create(prepared, observedAt);
    var later = ObservationTime.Create(observedAt.Value.AddTicks(1));

    bool recorded = format.RecordSnapshot(Money.Create(2.49m), later);

    Assert.True(recorded);
  }

  [Fact]
  public void RecordSnapshot_ReplacesLatestSnapshot_WhenPriceChanges() {
    var observedAt = ObservationTime.Create(DateTime.UtcNow);
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var format = ProductFormat.Create(prepared, observedAt);
    PriceSnapshot initial = format.LatestSnapshot;
    var later = ObservationTime.Create(observedAt.Value.AddTicks(1));

    _ = format.RecordSnapshot(Money.Create(2.49m), later);

    Assert.NotSame(initial, format.LatestSnapshot);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  public void RecordSnapshot_ReturnsFalse_WhenPriceIsUnchanged(int daysLater) {
    var observedAt = ObservationTime.Create(DateTime.UtcNow);
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var format = ProductFormat.Create(prepared, observedAt);
    var incoming = ObservationTime.Create(observedAt.Value.AddDays(daysLater));

    bool recorded = format.RecordSnapshot(prepared.Price, incoming);

    Assert.False(recorded);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  public void RecordSnapshot_PreservesLatestSnapshot_WhenPriceIsUnchanged(int daysLater) {
    var observedAt = ObservationTime.Create(DateTime.UtcNow);
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var format = ProductFormat.Create(prepared, observedAt);
    PriceSnapshot initial = format.LatestSnapshot;
    var incoming = ObservationTime.Create(observedAt.Value.AddDays(daysLater));

    _ = format.RecordSnapshot(prepared.Price, incoming);

    Assert.Same(initial, format.LatestSnapshot);
  }

  [Theory]
  [InlineData(1.99)] // Same price
  [InlineData(2.49)] // Different price
  public void RecordSnapshot_RejectsEarlierObservation(decimal price) {
    var observedAt = ObservationTime.Create(DateTime.UtcNow);
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var format = ProductFormat.Create(prepared, observedAt);
    var earlier = ObservationTime.Create(observedAt.Value.AddTicks(-1));

    void action() => format.RecordSnapshot(Money.Create(price), earlier);

    Assert.Throws<EarlierPriceObservationException>(action);
  }

  [Fact]
  public void RecordSnapshot_RejectsConflictingPriceAtSameTime() {
    var observedAt = ObservationTime.Create(DateTime.UtcNow);
    var prepared = new PreparedFormatObservation(
      Quantity.Create(1, "l"),
      Money.Create(1.99m),
      new ImageUrl(new Uri("https://example.com/milk.png")));
    var format = ProductFormat.Create(prepared, observedAt);

    void action() => format.RecordSnapshot(Money.Create(2.49m), observedAt);

    Assert.Throws<ConflictingPriceObservationException>(action);
  }
}