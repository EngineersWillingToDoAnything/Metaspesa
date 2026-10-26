using Metaspesa.Domain.Markets;

namespace Metaspesa.Domain.UnitTests.Markets;

public static class BrandTests {
  [Fact]
  public static void Create_GeneratesVersion7Id() {
    // Arrange
    string name = "Brand";

    // Act
    var aggregate = Brand.Create(name);

    // Assert
    Assert.Equal(7, aggregate.Id.Value.Version);
  }

  [Fact]
  public static void Create_GeneratesDistinctIds() {
    // Arrange
    var first = Brand.Create("Brand");

    // Act
    var second = Brand.Create("Brand");

    // Assert
    Assert.NotEqual(first.Id, second.Id);
  }

  [Fact]
  public static void Rehydrate_PreservesId() {
    // Arrange
    var id = new BrandId(Guid.Parse("00000000-0000-7000-8000-000000000001"));
    var name = new BrandName("Brand");

    // Act
    var aggregate = Brand.Rehydrate(id, name);

    // Assert
    Assert.Equal(id, aggregate.Id);
  }
}