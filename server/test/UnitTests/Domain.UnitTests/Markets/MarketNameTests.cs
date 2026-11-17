using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.UnitTests.Markets;

public static class MarketNameTests {
  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  [InlineData(" Legacy Market ")]
  public static void Constructor_PreservesStoredTextWithoutValidationOrTrimming(string storedName) {
    // Act
    var name = new MarketName(storedName);

    // Assert
    Assert.Equal(storedName, name.Value);
  }

  [Fact]
  public static void Create_TrimsWhitespace() {
    // Arrange
    string rawName = " Market ";

    // Act
    var name = MarketName.Create(rawName);

    // Assert
    Assert.Equal("Market", name.Value);
  }

  [Fact]
  public static void Create_PreservesDisplayCasing() {
    // Arrange
    string rawName = "Campofrío";

    // Act
    var name = MarketName.Create(rawName);

    // Assert
    Assert.Equal(rawName, name.Value);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" ")]
  public static void Create_RejectsBlankName(string? rawName) {
    // Act
    Exception? exception = Record.Exception(() => MarketName.Create(rawName!));

    // Assert
    Assert.IsType<InvalidMarketNameException>(exception);
  }

  [Fact]
  public static void Equality_IgnoresCase() {
    // Arrange
    var left = MarketName.Create("Market");
    var right = MarketName.Create("MARKET");

    // Act
    bool equal = left.Equals(right);

    // Assert
    Assert.True(equal);
  }

  [Fact]
  public static void Equality_RecognizesEquivalentUnicodeText() {
    // Arrange
    var left = MarketName.Create("Café");
    var right = MarketName.Create("CAFE\u0301");

    // Act
    bool equal = left.Equals(right);

    // Assert
    Assert.True(equal);
  }

  [Fact]
  public static void Equality_PreservesAccentDifferences() {
    // Arrange
    var left = MarketName.Create("Cafe");
    var right = MarketName.Create("Café");

    // Act
    bool equal = left.Equals(right);

    // Assert
    Assert.False(equal);
  }

  [Fact]
  public static void HashCode_MatchesForEqualNames() {
    // Arrange
    var left = MarketName.Create("Market");
    var right = MarketName.Create("MARKET");
    int expected = left.GetHashCode();

    // Act
    int actual = right.GetHashCode();

    // Assert
    Assert.Equal(expected, actual);
  }
}