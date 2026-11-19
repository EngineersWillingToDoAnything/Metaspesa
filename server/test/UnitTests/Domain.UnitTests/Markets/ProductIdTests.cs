using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.UnitTests.Markets;

public static class ProductIdTests {
  [Fact(DisplayName = "Creates and compares product ids by value")]
  public static void ProductId_CreatedAndEqual_WhenValueIsPositive() {
    var rawId = Guid.Parse("00000000-0000-7000-8000-000000000001");
    var id = ProductId.Create(rawId);

    Assert.Equal(rawId, id.Value);
    Assert.Equal(id, ProductId.Create(rawId));
  }

  [Fact(DisplayName = "Rejects empty product id")]
  public static void ProductId_ThrowsSpecificException_WhenValueIsInvalid() =>
    Assert.Throws<InvalidProductIdException>(() => ProductId.Create(Guid.Empty));
}