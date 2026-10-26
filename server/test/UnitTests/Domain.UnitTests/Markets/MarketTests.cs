using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.UnitTests.Markets;

public static class MarketTests {
  [Fact]
  public static void Create_GeneratesVersion7Id() {
    // Arrange
    string name = "Market";

    // Act
    var aggregate = Market.Create(name);

    // Assert
    Assert.Equal(7, aggregate.Id.Value.Version);
  }

  [Fact]
  public static void Create_GeneratesDistinctIds() {
    // Arrange
    var first = Market.Create("Market");

    // Act
    var second = Market.Create("Market");

    // Assert
    Assert.NotEqual(first.Id, second.Id);
  }

  [Fact]
  public static void Rehydrate_PreservesId() {
    // Arrange
    var id = new MarketId(Guid.Parse("00000000-0000-7000-8000-000000000001"));
    var name = new MarketName("Market");

    // Act
    var aggregate = Market.Hydrate(id, name, null);

    // Assert
    Assert.Equal(id, aggregate.Id);
  }

  [Fact]
  public static void Create_PreservesLogo() {
    // Arrange
    var logo = new Uri("https://example.test/logo.png");

    // Act
    var market = Market.Create("Market", logo);

    // Assert
    Assert.Equal(logo, market.LogoUrl!.Value.Value);
  }

  [Fact]
  public static void Create_AllowsAbsentLogo() {
    // Arrange
    string name = "Market";

    // Act
    var market = Market.Create(name);

    // Assert
    Assert.Null(market.LogoUrl);
  }
}