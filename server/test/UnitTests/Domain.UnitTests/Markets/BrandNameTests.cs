using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.UnitTests.Markets;

public static class BrandNameTests {
  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  [InlineData(" Legacy Brand ")]
  public static void Constructor_PreservesStoredTextWithoutValidationOrTrimming(string storedName) {
    // Act
    var name = new BrandName(storedName);

    // Assert
    Assert.Equal(storedName, name.Value);
  }

  [Fact]
  public static void Create_TrimsWhitespace() {
    // Arrange
    string rawName = " Brand ";

    // Act
    var name = BrandName.Create(rawName);

    // Assert
    Assert.Equal("Brand", name.Value);
  }

  [Fact]
  public static void Create_PreservesDisplayCasing() {
    // Arrange
    string rawName = "Campofrío";

    // Act
    var name = BrandName.Create(rawName);

    // Assert
    Assert.Equal(rawName, name.Value);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" ")]
  public static void Create_RejectsBlankName(string? rawName) {
    // Arrange
    // The theory supplies invalid primitive names.

    // Act
    Exception? exception = Record.Exception(() => BrandName.Create(rawName!));

    // Assert
    Assert.IsType<InvalidBrandNameException>(exception);
  }

  [Fact]
  public static void Equality_IgnoresCase() {
    // Arrange
    var left = BrandName.Create("Brand");
    var right = BrandName.Create("BRAND");

    // Act
    bool equal = left.Equals(right);

    // Assert
    Assert.True(equal);
  }

  [Fact]
  public static void Equality_RecognizesEquivalentUnicodeText() {
    // Arrange
    var left = BrandName.Create("Café");
    var right = BrandName.Create("CAFE\u0301");

    // Act
    bool equal = left.Equals(right);

    // Assert
    Assert.True(equal);
  }

  [Fact]
  public static void Equality_PreservesAccentDifferences() {
    // Arrange
    var left = BrandName.Create("Cafe");
    var right = BrandName.Create("Café");

    // Act
    bool equal = left.Equals(right);

    // Assert
    Assert.False(equal);
  }

  [Fact]
  public static void HashCode_MatchesForEqualNames() {
    // Arrange
    var left = BrandName.Create("Brand");
    var right = BrandName.Create("BRAND");
    int expected = left.GetHashCode();

    // Act
    int actual = right.GetHashCode();

    // Assert
    Assert.Equal(expected, actual);
  }
}