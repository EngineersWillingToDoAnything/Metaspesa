using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.UnitTests.Markets;

public class ProductTests {
  [Fact]
  public void Create_IncludesInitialFormat() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var formatObservation = new ProductFormatObservationParams(
      1, "l", 1.99m, new Uri("https://example.com/milk.png"));
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn, [formatObservation]);

    // Act
    var product = Product.Create(observation);

    // Assert
    Assert.True(product.OffersFormat(formatObservation));
  }

  [Fact]
  public void Create_TracksInitialFormat() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var formatObservation = new ProductFormatObservationParams(
      1, "l", 1.99m, new Uri("https://example.com/milk.png"));
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn, [formatObservation]);

    // Act
    var product = Product.Create(observation);

    // Assert
    Assert.True(product.HasPendingFormat(formatObservation));
  }

  [Fact]
  public void Create_TracksInitialSnapshot() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var formatObservation = new ProductFormatObservationParams(
      1, "l", 1.99m, new Uri("https://example.com/milk.png"));
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn, [formatObservation]);

    // Act
    var product = Product.Create(observation);

    // Assert
    Assert.True(product.HasPendingPriceObservation(formatObservation, observedOn));
  }

  [Fact]
  public void Create_IncludesAllObservedFormats() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [
        new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
        new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png"))
      ]);

    // Act
    var product = Product.Create(observation);

    // Assert
    Assert.All(observation.Formats, format =>
      Assert.True(product.OffersFormat(format)));
  }

  [Fact]
  public void Create_TracksAllInitialFormats() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [
        new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
        new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png"))
      ]);

    // Act
    var product = Product.Create(observation);

    // Assert
    Assert.All(observation.Formats, format =>
      Assert.True(product.HasPendingFormat(format)));
  }

  [Fact]
  public void Create_TracksAllInitialSnapshots() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [
        new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
        new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png"))
      ]);

    // Act
    var product = Product.Create(observation);

    // Assert
    Assert.All(observation.Formats, format =>
      Assert.True(product.HasPendingPriceObservation(format, observation.ObservedOn)));
  }

  [Fact]
  public void Create_RejectsNullObservation() {
    // Act
    static void action() => Product.Create(null!);

    // Assert
    Assert.Throws<ArgumentNullException>(action);
  }

  [Fact]
  public void Create_RejectsEmptyFormats() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn, []);

    // Act
    void action() => Product.Create(observation);

    // Assert
    Assert.Throws<EmptyProductObservationException>(action);
  }

  [Fact]
  public void Create_RejectsMissingFormatCollection() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn, null!);

    // Act
    void action() => Product.Create(observation);

    // Assert
    Assert.Throws<EmptyProductObservationException>(action);
  }

  [Fact]
  public void Create_RejectsExactDuplicateFormat() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [
        new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
        new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))
      ]);

    // Act
    void action() => Product.Create(observation);

    // Assert
    Assert.Throws<DuplicateObservedProductFormatException>(action);
  }

  [Fact]
  public void Create_RejectsDuplicateFormatWithEquivalentUnits() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [
        new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
        new ProductFormatObservationParams(1000, "ml", 1.99m, new Uri("https://example.com/milk.png"))
      ]);

    // Act
    void action() => Product.Create(observation);

    // Assert
    Assert.Throws<DuplicateObservedProductFormatException>(action);
  }

  [Fact]
  public void Create_RejectsDuplicateFormatWithConflictingPrice() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [
        new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
        new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/milk.png"))
      ]);

    // Act
    void action() => Product.Create(observation);

    // Assert
    Assert.Throws<DuplicateObservedProductFormatException>(action);
  }

  [Fact]
  public void Create_RejectsDuplicateFormatWithConflictingImage() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [
        new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
        new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/other.png"))
      ]);

    // Act
    void action() => Product.Create(observation);

    // Assert
    Assert.Throws<DuplicateObservedProductFormatException>(action);
  }

  [Fact]
  public void RegisterObservation_RejectsNullObservation() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);

    // Act
    void action() => product.RegisterObservation(null!);

    // Assert
    Assert.Throws<ArgumentNullException>(action);
  }

  [Fact]
  public void RegisterObservation_RejectsDifferentMarket() {
    // Arrange
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

    // Act
    void action() => product.RegisterObservation(observation);

    // Assert
    Assert.Throws<ProductObservationIdentityMismatchException>(action);
  }

  [Fact]
  public void RegisterObservation_RejectsDifferentBrand() {
    // Arrange
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

    // Act
    void action() => product.RegisterObservation(observation);

    // Assert
    Assert.Throws<ProductObservationIdentityMismatchException>(action);
  }

  [Fact]
  public void RegisterObservation_RejectsDifferentName() {
    // Arrange
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

    // Act
    void action() => product.RegisterObservation(observation);

    // Assert
    Assert.Throws<ProductObservationIdentityMismatchException>(action);
  }

  [Fact]
  public void RegisterObservation_RejectsDuplicateFormats() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [
        new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
        new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))
      ]);

    // Act
    void action() => product.RegisterObservation(observation);

    // Assert
    Assert.Throws<DuplicateObservedProductFormatException>(action);
  }

  [Fact]
  public void RegisterObservation_TracksSnapshot_WhenFormatRecordsChange() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);

    var formatObservation = new ProductFormatObservationParams(
      1, "l", 2.49m, new Uri("https://example.com/milk.png"));
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [formatObservation]);

    // Act
    product.RegisterObservation(observation);

    // Assert
    Assert.True(product.HasPendingPriceObservation(formatObservation, observation.ObservedOn));
  }

  [Fact]
  public void RegisterObservation_DoesNotTrackExistingFormatAgain() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    int pendingFormats = product.CountPendingFormats();

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/milk.png"))]);

    // Act
    product.RegisterObservation(observation);

    // Assert
    Assert.Equal(pendingFormats, product.CountPendingFormats());
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  public void RegisterObservation_LeavesPendingSnapshotsUnchanged_WhenFormatRecordsNothing(
    int daysLater
  ) {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    int pendingObservations = product.CountPendingPriceObservations();

    ProductObservationParams observation = initialObservation with {
      ObservedOn = observedOn.AddDays(daysLater)
    };

    // Act
    product.RegisterObservation(observation);

    // Assert
    Assert.Equal(pendingObservations, product.CountPendingPriceObservations());
  }

  [Fact]
  public void RegisterObservation_AddsNewFormat() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);

    var formatObservation = new ProductFormatObservationParams(
      2, "l", 3.49m, new Uri("https://example.com/large.png"));
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [formatObservation]);

    // Act
    product.RegisterObservation(observation);

    // Assert
    Assert.True(product.OffersFormat(formatObservation));
  }

  [Fact]
  public void RegisterObservation_TracksNewFormat() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);

    var formatObservation = new ProductFormatObservationParams(
      2, "l", 3.49m, new Uri("https://example.com/large.png"));
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [formatObservation]);

    // Act
    product.RegisterObservation(observation);

    // Assert
    Assert.True(product.HasPendingFormat(formatObservation));
  }

  [Fact]
  public void RegisterObservation_TracksInitialSnapshotOfNewFormat() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);

    var formatObservation = new ProductFormatObservationParams(
      2, "l", 3.49m, new Uri("https://example.com/large.png"));
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [formatObservation]);

    // Act
    product.RegisterObservation(observation);

    // Assert
    Assert.True(product.HasPendingPriceObservation(formatObservation, observation.ObservedOn));
  }

  [Fact]
  public void RegisterObservation_ReusesExistingFormatForEquivalentIdentity() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    int formatCount = product.CountFormats();

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1000, "ml", 1.99m, new Uri("https://example.com/milk.png"))]);

    // Act
    product.RegisterObservation(observation);

    // Assert
    Assert.Equal(formatCount, product.CountFormats());
  }

  [Fact]
  public void RegisterObservation_ReusesExistingFormat_WhenImageChanges() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);
    int formatCount = product.CountFormats();

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/other.png"))]);

    // Act
    product.RegisterObservation(observation);

    // Assert
    Assert.Equal(formatCount, product.CountFormats());
  }

  [Fact]
  public void RegisterObservation_TracksOnlyRecordedSnapshots_ForMixedObservation() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png"))]);
    var product = Product.Create(initialObservation);
    int pendingObservations = product.CountPendingPriceObservations();

    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(2, "l", 3.99m, new Uri("https://example.com/large.png")),
       new ProductFormatObservationParams(3, "l", 4.49m, new Uri("https://example.com/new.png"))]);

    // Act
    product.RegisterObservation(observation);

    // Assert
    Assert.Equal(pendingObservations + 2, product.CountPendingPriceObservations());
  }

  [Fact]
  public void RegisterObservation_TracksOnlyNewFormat_ForMixedObservation() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png"))]);
    var product = Product.Create(initialObservation);

    var newFormatObservation = new ProductFormatObservationParams(
      3, "l", 4.49m, new Uri("https://example.com/new.png"));
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
       new ProductFormatObservationParams(2, "l", 3.99m, new Uri("https://example.com/large.png")),
       newFormatObservation]);

    // Act
    product.RegisterObservation(observation);

    // Assert
    Assert.True(product.HasPendingFormat(newFormatObservation));
  }

  [Fact]
  public void RegisterObservation_RetainsEveryRecordedSnapshot() {
    // Arrange
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    var observedOn = new DateOnly(2026, 7, 28);
    var initialObservation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn,
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(initialObservation);

    var formatObservation = new ProductFormatObservationParams(
      1, "l", 2.49m, new Uri("https://example.com/milk.png"));
    var observation = new ProductObservationParams(
      marketId, brandId, "Milk", observedOn.AddDays(1),
      [formatObservation]);
    product.RegisterObservation(observation);
    ProductObservationParams next = initialObservation with {
      ObservedOn = observedOn.AddDays(2)
    };

    // Act
    product.RegisterObservation(next);

    // Assert
    Assert.True(product.HasPendingPriceObservation(formatObservation, observation.ObservedOn));
  }

  [Fact]
  public void Rehydrate_PreservesState() {
    // Arrange
    var observation = new ProductObservationParams(
      Guid.CreateVersion7(), Guid.CreateVersion7(), "Milk", new DateOnly(2026, 7, 28),
      [new(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var original = Product.Create(observation);

    // Act
    var restored = Product.Rehydrate(original.ToPrimitives());

    // Assert
    Assert.Equivalent(original, restored);
  }

  [Fact]
  public void Rehydrate_DoesNotRaiseDomainEvents() {
    // Arrange
    var observation = new ProductObservationParams(
      Guid.CreateVersion7(), Guid.CreateVersion7(), "Milk", new DateOnly(2026, 7, 28),
      [new(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var original = Product.Create(observation);

    // Act
    var restored = Product.Rehydrate(original.ToPrimitives());

    // Assert
    Assert.Empty(restored.DomainEvents);
  }

  [Fact]
  public void Rehydrate_DoesNotTrackPersistedFormatsAsNew() {
    // Arrange
    var observation = new ProductObservationParams(
      Guid.CreateVersion7(), Guid.CreateVersion7(), "Milk", new DateOnly(2026, 7, 28),
      [new(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var original = Product.Create(observation);

    // Act
    var restored = Product.Rehydrate(original.ToPrimitives());

    // Assert
    Assert.Equal(0, restored.CountPendingFormats());
  }

  [Fact]
  public void Rehydrate_DoesNotTrackPersistedPricesAsNew() {
    // Arrange
    var observation = new ProductObservationParams(
      Guid.CreateVersion7(), Guid.CreateVersion7(), "Milk", new DateOnly(2026, 7, 28),
      [new(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var original = Product.Create(observation);

    // Act
    var restored = Product.Rehydrate(original.ToPrimitives());

    // Assert
    Assert.Equal(0, restored.CountPendingPriceObservations());
  }
}