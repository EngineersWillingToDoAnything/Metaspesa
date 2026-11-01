using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.UnitTests.Markets;

public static class MarketNameTests {
  [Fact]
  public static void Constructor_TrimsWhitespace() {
    // Arrange
    string rawName = " Market ";

    // Act
    var name = new MarketName(rawName);

    // Assert
    Assert.Equal("Market", name.Value);
  }

  [Fact]
  public static void Constructor_PreservesDisplayCasing() {
    // Arrange
    string rawName = "Campofrío";

    // Act
    var name = new MarketName(rawName);

    // Assert
    Assert.Equal(rawName, name.Value);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" ")]
  public static void Constructor_RejectsBlankName(string? rawName) {
    // Act
    Exception? exception = Record.Exception(() => new MarketName(rawName!));

    // Assert
    Assert.IsType<InvalidMarketNameException>(exception);
  }

  [Fact]
  public static void Equality_IgnoresCase() {
    // Arrange
    var left = new MarketName("Market");
    var right = new MarketName("MARKET");

    // Act
    bool equal = left.Equals(right);

    // Assert
    Assert.True(equal);
  }

  [Fact]
  public static void Equality_RecognizesEquivalentUnicodeText() {
    // Arrange
    var left = new MarketName("Café");
    var right = new MarketName("CAFE\u0301");

    // Act
    bool equal = left.Equals(right);

    // Assert
    Assert.True(equal);
  }

  [Fact]
  public static void Equality_PreservesAccentDifferences() {
    // Arrange
    var left = new MarketName("Cafe");
    var right = new MarketName("Café");

    // Act
    bool equal = left.Equals(right);

    // Assert
    Assert.False(equal);
  }

  [Fact]
  public static void HashCode_MatchesForEqualNames() {
    // Arrange
    var left = new MarketName("Market");
    var right = new MarketName("MARKET");
    int expected = left.GetHashCode();

    // Act
    int actual = right.GetHashCode();

    // Assert
    Assert.Equal(expected, actual);
  }
}