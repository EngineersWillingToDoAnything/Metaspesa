using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.UnitTests.Markets;

public static class BrandIdTests {
  [Fact]
  public static void Constructor_RejectsEmptyGuid() {
    // Arrange
    Guid value = Guid.Empty;

    // Act
    Exception? exception = Record.Exception(() => new BrandId(value));

    // Assert
    Assert.IsType<InvalidBrandIdException>(exception);
  }

  [Fact]
  public static void Constructor_PreservesGuid() {
    // Arrange
    var expected = Guid.Parse("00000000-0000-7000-8000-000000000001");

    // Act
    var id = new BrandId(expected);

    // Assert
    Assert.Equal(expected, id.Value);
  }
}