using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.UnitTests.Markets;

public class ProductTests {
  [Fact]
  public void Rehydrate_DoesNotRaiseDomainEvents() {
    // Arrange
    var id = new ProductId(Guid.CreateVersion7());
    var brandId = new BrandId(Guid.CreateVersion7());
    var marketId = new MarketId(Guid.CreateVersion7());
    var name = new ProductName("Milk");

    // Act
    var product = Product.Rehydrate(id, name, brandId, marketId, []);

    // Assert
    Assert.Empty(product.DomainEvents);
  }

  [Fact]
  public void Create_GeneratesVersion7Id() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);

    var product = Product.Create(observation);

    Assert.Equal(7, product.Id.Value.Version);
  }

  [Fact]
  public void Create_IncludesInitialFormat() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);

    var product = Product.Create(observation);

    Assert.Single(product.Formats);
  }

  [Fact]
  public void Create_TracksInitialFormat() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);

    var product = Product.Create(observation);

    Assert.Same(product.Formats.Single(), product.PendingFormats.Single());
  }

  [Fact]
  public void Create_TracksInitialSnapshot() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);

    var product = Product.Create(observation);

    Assert.Same(product.Formats.Single().LatestSnapshot, product.PendingSnapshots.Single());
  }

  [Fact]
  public void Create_IncludesAllObservedFormats() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png"))]);

    var product = Product.Create(observation);

    Assert.Equal(2, product.Formats.Count);
  }

  [Fact]
  public void Create_TracksAllInitialFormats() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png"))]);

    var product = Product.Create(observation);

    Assert.Equal(product.Formats, product.PendingFormats);
  }

  [Fact]
  public void Create_TracksAllInitialSnapshots() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png"))]);

    var product = Product.Create(observation);

    Assert.Equal(
      product.Formats.Select(format => format.LatestSnapshot),
      product.PendingSnapshots);
  }

  [Fact]
  public void Create_RejectsNullObservation() {
    static void action() => Product.Create(null!);

    Assert.Throws<ArgumentNullException>(action);
  }

  [Fact]
  public void Create_RejectsEmptyFormats() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn, []);

    void action() => Product.Create(observation);

    Assert.Throws<EmptyProductObservationException>(action);
  }

  [Fact]
  public void Create_RejectsMissingFormatCollection() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn, null!);

    void action() => Product.Create(observation);

    Assert.Throws<EmptyProductObservationException>(action);
  }

  [Fact]
  public void Create_RejectsExactDuplicateFormat() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);

    void action() => Product.Create(observation);

    Assert.Throws<DuplicateObservedProductFormatException>(action);
  }

  [Fact]
  public void Create_RejectsDuplicateFormatWithEquivalentUnits() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(1000, "ml", 1.99m, new Uri("https://example.com/milk.png"))]);

    void action() => Product.Create(observation);

    Assert.Throws<DuplicateObservedProductFormatException>(action);
  }

  [Fact]
  public void Create_RejectsDuplicateFormatWithConflictingPrice() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/milk.png"))]);

    void action() => Product.Create(observation);

    Assert.Throws<DuplicateObservedProductFormatException>(action);
  }

  [Fact]
  public void Create_RejectsDuplicateFormatWithConflictingImage() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/other.png"))]);

    void action() => Product.Create(observation);

    Assert.Throws<DuplicateObservedProductFormatException>(action);
  }

  [Fact]
  public void RegisterObservation_RejectsNullObservation() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);

    void action() => product.RegisterObservation(null!);

    Assert.Throws<ArgumentNullException>(action);
  }

  [Fact]
  public void RegisterObservation_RejectsDifferentMarket() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    ProductObservationParams observation = initialObservation with {
      MarketId = Guid.CreateVersion7()
    };

    void action() => product.RegisterObservation(observation);

    Assert.Throws<ProductObservationIdentityMismatchException>(action);
  }

  [Fact]
  public void RegisterObservation_RejectsDifferentBrand() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    ProductObservationParams observation = initialObservation with {
      BrandId = Guid.CreateVersion7()
    };

    void action() => product.RegisterObservation(observation);

    Assert.Throws<ProductObservationIdentityMismatchException>(action);
  }

  [Fact]
  public void RegisterObservation_RejectsDifferentName() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    ProductObservationParams observation = initialObservation with {
      ProductName = "Bread"
    };

    void action() => product.RegisterObservation(observation);

    Assert.Throws<ProductObservationIdentityMismatchException>(action);
  }

  [Fact]
  public void RegisterObservation_AcceptsEquivalentIdentity() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    ProductObservationParams observation = initialObservation with {
      ProductName = "MILK"
    };

    Exception? error = Record.Exception(() => product.RegisterObservation(observation));

    Assert.Null(error);
  }

  [Fact]
  public void RegisterObservation_RejectsDuplicateFormats() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);

    void action() => product.RegisterObservation(observation);

    Assert.Throws<DuplicateObservedProductFormatException>(action);
  }

  [Fact]
  public void RegisterObservation_TracksSnapshot_WhenFormatRecordsChange() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    PriceSnapshot initialSnapshot = product.PendingSnapshots.Single();

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/milk.png"))]);

    product.RegisterObservation(observation);

    Assert.Equal(
      [initialSnapshot, product.Formats.Single().LatestSnapshot],
      product.PendingSnapshots);
  }

  [Fact]
  public void RegisterObservation_DoesNotTrackExistingFormatAgain() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    ProductFormat original = product.PendingFormats.Single();

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/milk.png"))]);

    product.RegisterObservation(observation);

    Assert.Same(original, product.PendingFormats.Single());
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  public void RegisterObservation_LeavesPendingSnapshotsUnchanged_WhenFormatRecordsNothing(int daysLater) {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    PriceSnapshot initialSnapshot = product.PendingSnapshots.Single();

    ProductObservationParams observation = initialObservation with {
      ObservedOn = observedOn.AddDays(daysLater)
    };

    product.RegisterObservation(observation);

    Assert.Same(initialSnapshot, product.PendingSnapshots.Single());
  }

  [Fact]
  public void RegisterObservation_AddsNewFormat() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png"))]);

    product.RegisterObservation(observation);

    Assert.Equal(2, product.Formats.Count);
  }

  [Fact]
  public void RegisterObservation_TracksNewFormat() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    ProductFormat original = product.Formats.Single();

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png"))]);

    product.RegisterObservation(observation);

    Assert.Equal([original, product.Formats.Last()], product.PendingFormats);
  }

  [Fact]
  public void RegisterObservation_TracksInitialSnapshotOfNewFormat() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    PriceSnapshot original = product.PendingSnapshots.Single();

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png"))]);

    product.RegisterObservation(observation);

    Assert.Equal(
      [original, product.Formats.Last().LatestSnapshot],
      product.PendingSnapshots);
  }

  [Fact]
  public void RegisterObservation_RetainsUnobservedFormat() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    ProductFormat original = product.Formats.Single();

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png"))]);

    product.RegisterObservation(observation);

    Assert.Contains(original, product.Formats);
  }

  [Fact]
  public void RegisterObservation_ReusesExistingFormatForEquivalentIdentity() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    ProductFormat original = product.Formats.Single();

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1000, "ml", 1.99m, new Uri("https://example.com/milk.png"))]);

    product.RegisterObservation(observation);

    Assert.Same(original, product.Formats.Single());
  }

  [Fact]
  public void RegisterObservation_ReusesExistingFormat_WhenImageChanges() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    ProductFormat original = product.Formats.Single();

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/other.png"))]);

    product.RegisterObservation(observation);

    Assert.Same(original, product.Formats.Single());
  }

  [Fact]
  public void RegisterObservation_TracksOnlyRecordedSnapshots_ForMixedObservation() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png"))]);
    var product = Product.Create(initialObservation);
    ProductFormat changedFormat = product.Formats.Last();

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(2, "l", 3.99m, new Uri("https://example.com/large.png")),
       new ProductFormatObservationParams(3, "l", 4.49m, new Uri("https://example.com/new.png"))]);

    product.RegisterObservation(observation);

    Assert.Equal(
      [changedFormat.LatestSnapshot, product.Formats.Last().LatestSnapshot],
      product.PendingSnapshots.Skip(2));
  }

  [Fact]
  public void RegisterObservation_TracksOnlyNewFormat_ForMixedObservation() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png"))]);
    var product = Product.Create(initialObservation);

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(2, "l", 3.99m, new Uri("https://example.com/large.png")),
       new ProductFormatObservationParams(3, "l", 4.49m, new Uri("https://example.com/new.png"))]);

    product.RegisterObservation(observation);

    Assert.Same(product.Formats.Last(), product.PendingFormats.Skip(2).Single());
  }

  [Fact]
  public void RegisterObservation_PreservesPendingFormats_WhenDuplicatesAreRejected() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    ProductFormat[] before = [.. product.PendingFormats];

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);

    _ = Record.Exception(() => product.RegisterObservation(observation));

    Assert.Equal(before, product.PendingFormats);
  }

  [Fact]
  public void RegisterObservation_PreservesPendingSnapshots_WhenDuplicatesAreRejected() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    PriceSnapshot[] before = [.. product.PendingSnapshots];

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);

    _ = Record.Exception(() => product.RegisterObservation(observation));

    Assert.Equal(before, product.PendingSnapshots);
  }

  [Fact]
  public void RegisterObservation_PreservesFormatMembership_WhenPreparationFails() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    ProductFormat[] before = [.. product.Formats];

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png")),
       null!]);

    _ = Record.Exception(() => product.RegisterObservation(observation));

    Assert.Equal(before, product.Formats);
  }

  [Fact]
  public void RegisterObservation_PreservesPendingFormats_WhenPreparationFails() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    ProductFormat[] before = [.. product.PendingFormats];

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png")),
       null!]);

    _ = Record.Exception(() => product.RegisterObservation(observation));

    Assert.Equal(before, product.PendingFormats);
  }

  [Fact]
  public void RegisterObservation_PreservesPendingSnapshots_WhenPreparationFails() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    PriceSnapshot[] before = [.. product.PendingSnapshots];

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png")),
       null!]);

    _ = Record.Exception(() => product.RegisterObservation(observation));

    Assert.Equal(before, product.PendingSnapshots);
  }

  [Fact]
  public void RegisterObservation_RetainsEveryRecordedSnapshot() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/milk.png"))]);
    product.RegisterObservation(observation);
    PriceSnapshot changedSnapshot = product.Formats.Single().LatestSnapshot;
    ProductObservationParams next = initialObservation with {
      ObservedOn = observedOn.AddDays(2)
    };

    product.RegisterObservation(next);

    Assert.Equal(
      [changedSnapshot, product.Formats.Single().LatestSnapshot],
      product.PendingSnapshots.Skip(1));
  }

  [Fact]
  public void Rehydrate_PreservesProductId() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var original = Product.Create(observation);

    var product = Product.Rehydrate(
      original.Id, original.Name, original.BrandId, original.MarketId, original.Formats);

    Assert.Equal(original.Id, product.Id);
  }

  [Fact]
  public void Rehydrate_PreservesFormats() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var original = Product.Create(observation);

    var product = Product.Rehydrate(
      original.Id, original.Name, original.BrandId, original.MarketId, original.Formats);

    Assert.Equal(original.Formats, product.Formats);
  }

  [Fact]
  public void Rehydrate_RecordsNoPendingFormat() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var original = Product.Create(observation);

    var product = Product.Rehydrate(
      original.Id, original.Name, original.BrandId, original.MarketId, original.Formats);

    Assert.Empty(product.PendingFormats);
  }

  [Fact]
  public void Rehydrate_RecordsNoPendingSnapshot() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var original = Product.Create(observation);
    ProductFormat originalFormat = original.Formats.Single();
    var restoredFormat = ProductFormat.Rehydrate(
      originalFormat.Id, originalFormat.Quantity, originalFormat.ImageUrl, originalFormat.LatestSnapshot);

    var product = Product.Rehydrate(
      original.Id, original.Name, original.BrandId, original.MarketId, [restoredFormat]);

    Assert.Empty(product.PendingSnapshots);
  }

  [Fact]
  public void Rehydrate_CopiesFormatCollection() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var original = Product.Create(observation);
    var formats = new List<ProductFormat>(original.Formats);
    var product = Product.Rehydrate(
      original.Id, original.Name, original.BrandId, original.MarketId, formats);

    formats.Clear();

    Assert.Equal(original.Formats, product.Formats);
  }

  [Fact]
  public void Formats_PreventsExternalCollectionMutation() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);

    void action() => ((ICollection<ProductFormat>)product.Formats).Clear();

    Assert.Throws<NotSupportedException>(action);
  }

  [Fact]
  public void PendingFormats_PreventsExternalCollectionMutation() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);

    void action() => ((ICollection<ProductFormat>)product.PendingFormats).Clear();

    Assert.Throws<NotSupportedException>(action);
  }

  [Fact]
  public void PendingSnapshots_PreventsExternalCollectionMutation() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);

    void action() => ((ICollection<PriceSnapshot>)product.PendingSnapshots).Clear();

    Assert.Throws<NotSupportedException>(action);
  }

  [Fact]
  public void ToPrimitives_ExportsCompleteSnapshot() {
    // Arrange
    var brandId = Guid.CreateVersion7();
    var marketId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 8, 19);
    var observation = new ProductObservationParams(
      marketId,
      brandId,
      "Milk",
      observedOn,
      [new ProductFormatObservationParams(1, "kg", 1.99m, new Uri("https://example.test/milk.png"))]);
    var product = Product.Create(observation);

    ProductFormat originalFormat = product.Formats.Single();
    var expectedSnapshot = new PriceSnapshotPrimitives(
      originalFormat.LatestSnapshot.Id.Value, 1.99m, observedOn);
    var expectedFormat = new ProductFormatPrimitives(originalFormat.Id.Value, 1000m, "g",
      "https://example.test/milk.png", expectedSnapshot, [expectedSnapshot]);

    var expected = new ProductPrimitives(product.Id.Value, "Milk", brandId, marketId,
      [expectedFormat], [originalFormat.Id.Value]);

    // Act
    ProductPrimitives state = product.ToPrimitives();

    // Assert
    Assert.Equivalent(expected, state, strict: true);
  }

  [Fact]
  public void ToPrimitives_PreservesEveryPendingPriceObservation() {
    // Arrange
    var brandId = Guid.CreateVersion7();
    var marketId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 8, 19);
    var observation = new ProductObservationParams(marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "kg", 1.99m, new Uri("https://example.test/milk.png"))]);

    var product = Product.Create(observation);
    product.RegisterObservation(observation with {
      ObservedOn = observedOn.AddDays(1),
      Formats = [new ProductFormatObservationParams(1, "kg", 2.49m, new Uri("https://example.test/milk.png"))],
    });

    ProductFormat originalFormat = product.Formats.Single();
    PriceSnapshot[] originalSnapshots = [.. originalFormat.PendingSnapshots];
    var firstSnapshot = new PriceSnapshotPrimitives(
      originalSnapshots[0].Id.Value, 1.99m, observedOn);
    var latestSnapshot = new PriceSnapshotPrimitives(
      originalSnapshots[1].Id.Value, 2.49m, observedOn.AddDays(1));

    var expected = new ProductFormatPrimitives(originalFormat.Id.Value, 1000m, "g",
      "https://example.test/milk.png", latestSnapshot, [firstSnapshot, latestSnapshot]);

    // Act
    ProductFormatPrimitives format = product.ToPrimitives().Formats.Single();

    // Assert
    Assert.Equivalent(expected, format, strict: true);
  }

  [Fact]
  public void Snapshot_RemainsUnchangedAfterAnotherObservation() {
    // Arrange
    var brandId = Guid.CreateVersion7();
    var marketId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 8, 19);
    var observation = new ProductObservationParams(marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "kg", 1.99m, new Uri("https://example.test/milk.png"))]);

    var product = Product.Create(observation);

    ProductPrimitives state = product.ToPrimitives();
    ProductFormat originalFormat = product.Formats.Single();
    var expectedSnapshot = new PriceSnapshotPrimitives(
      originalFormat.LatestSnapshot.Id.Value, 1.99m, observedOn);
    var expectedFormat = new ProductFormatPrimitives(
      originalFormat.Id.Value, 1000m, "g",
      "https://example.test/milk.png", expectedSnapshot, [expectedSnapshot]);

    var expected = new ProductPrimitives(product.Id.Value, "Milk", brandId, marketId,
      [expectedFormat], [originalFormat.Id.Value]);

    // Act
    product.RegisterObservation(observation with {
      ObservedOn = observedOn.AddDays(1),
      Formats = [
        new ProductFormatObservationParams(1, "kg", 2.49m, new Uri("https://example.test/milk.png")),
        new ProductFormatObservationParams(2, "kg", 3.99m, new Uri("https://example.test/milk.png")),
      ],
    });

    // Assert
    Assert.Equivalent(expected, state, strict: true);
  }

  [Fact]
  public void ToPrimitives_DoesNotConsumePendingFormats() {
    // Arrange
    var brandId = Guid.CreateVersion7();
    var marketId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 8, 19);
    var observation = new ProductObservationParams(marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "kg", 1.99m, new Uri("https://example.test/milk.png"))]);

    var product = Product.Create(observation);

    // Act
    _ = product.ToPrimitives();

    // Assert
    Assert.Single(product.PendingFormats);
  }

  [Fact]
  public void ToPrimitives_DoesNotConsumePendingSnapshots() {
    // Arrange
    var brandId = Guid.CreateVersion7();
    var marketId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 8, 19);
    var observation = new ProductObservationParams(marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "kg", 1.99m, new Uri("https://example.test/milk.png"))]);

    var product = Product.Create(observation);

    // Act
    _ = product.ToPrimitives();

    // Assert
    Assert.Single(product.PendingSnapshots);
  }
}