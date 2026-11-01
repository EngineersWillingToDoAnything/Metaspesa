using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.UnitTests.Markets;

public static class BrandNameTests {
  [Fact]
  public static void Constructor_TrimsWhitespace() {
    // Arrange
    string rawName = " Brand ";

    // Act
    var name = new BrandName(rawName);

    // Assert
    Assert.Equal("Brand", name.Value);
  }

  [Fact]
  public static void Constructor_PreservesDisplayCasing() {
    // Arrange
    string rawName = "Campofrío";

    // Act
    var name = new BrandName(rawName);

    // Assert
    Assert.Equal(rawName, name.Value);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" ")]
  public static void Constructor_RejectsBlankName(string? rawName) {
    // Arrange
    // The theory supplies invalid primitive names.

    // Act
    Exception? exception = Record.Exception(() => new BrandName(rawName!));

    // Assert
    Assert.IsType<InvalidBrandNameException>(exception);
  }

  [Fact]
  public static void Equality_IgnoresCase() {
    // Arrange
    var left = new BrandName("Brand");
    var right = new BrandName("BRAND");

    // Act
    bool equal = left.Equals(right);

    // Assert
    Assert.True(equal);
  }

  [Fact]
  public static void Equality_RecognizesEquivalentUnicodeText() {
    // Arrange
    var left = new BrandName("Café");
    var right = new BrandName("CAFE\u0301");

    // Act
    bool equal = left.Equals(right);

    // Assert
    Assert.True(equal);
  }

  [Fact]
  public static void Equality_PreservesAccentDifferences() {
    // Arrange
    var left = new BrandName("Cafe");
    var right = new BrandName("Café");

    // Act
    bool equal = left.Equals(right);

    // Assert
    Assert.False(equal);
  }

  [Fact]
  public static void HashCode_MatchesForEqualNames() {
    // Arrange
    var left = new BrandName("Brand");
    var right = new BrandName("BRAND");
    int expected = left.GetHashCode();

    // Act
    int actual = right.GetHashCode();

    // Assert
    Assert.Equal(expected, actual);
  }
}