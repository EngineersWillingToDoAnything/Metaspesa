using Metaspesa.Domain.Markets;

namespace Metaspesa.Domain.UnitTests.Markets;

public static class BrandTests {
  [Fact]
  public static void Create_GeneratesDistinctBrands() {
    // Arrange
    var first = Brand.Create("Brand");

    // Act
    var second = Brand.Create("Brand");

    // Assert
    Assert.NotEqual(first.Id, second.Id);
  }

  [Fact]
  public static void Rehydrate_PreservesState() {
    // Arrange
    var originalBrand = Brand.Create("Brand");

    // Act
    var aggregate = Brand.Rehydrate(originalBrand.ToPrimitives());

    // Assert
    Assert.Equivalent(originalBrand, aggregate);
  }
}