using Metaspesa.Domain.Markets;

namespace Metaspesa.Domain.UnitTests.Markets;

public static class MarketTests {
  [Fact]
  public static void Rehydrate_AcceptsPersistedValuesThatCreationRejects() {
    // Arrange
    var persisted = new MarketPrimitives(Guid.Empty, " ", "legacy/logo.png");

    // Act
    var market = Market.Rehydrate(persisted);

    // Assert
    Assert.Equal(persisted.Id, market.Id.Value);
  }

  private sealed class MarketLogos() : TheoryData<Uri?>(
    null,
    new Uri("https://example.test/logo.png")
  );

  [Theory]
  [ClassData<MarketLogos>]
  public static void Create_GeneratesDistinctMarkets(Uri? logo) {
    // Arrange
    const string name = "Market";
    var first = Market.Create(name, logo);

    // Act
    var second = Market.Create(name, logo);

    // Assert
    Assert.NotEqual(first.Id, second.Id);
  }

  [Theory]
  [ClassData<MarketLogos>]
  public static void Rehydrate_PreservesState(Uri? logo) {
    // Arrange
    var originalMarket = Market.Create("Market", logo);

    // Act
    var aggregate = Market.Rehydrate(originalMarket.ToPrimitives());

    // Assert
    Assert.Equivalent(originalMarket, aggregate);
  }
}