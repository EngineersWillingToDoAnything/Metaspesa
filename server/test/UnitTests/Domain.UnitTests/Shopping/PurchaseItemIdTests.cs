using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class PurchaseItemIdTests {
  [Fact]
  public void Create_PreservesIdentity() {
    var value = Guid.Parse("00000000-0000-7000-8000-000000000001");

    var id = PurchaseItemId.Create(value);

    Assert.Equal(value, id.Value);
  }

  [Fact]
  public void Create_RejectsEmptyId() {
    Assert.Throws<InvalidPurchaseItemIdException>(() =>
      PurchaseItemId.Create(Guid.Empty));
  }

  [Fact]
  public void Equals_UsesIdentityValue() {
    var value = Guid.Parse("00000000-0000-7000-8000-000000000001");

    Assert.Equal(PurchaseItemId.Create(value), PurchaseItemId.Create(value));
  }

  [Fact]
  public void ToString_ReturnsPrimitiveIdentity() {
    var value = Guid.Parse("00000000-0000-7000-8000-000000000001");
    var id = PurchaseItemId.Create(value);

    Assert.Equal(value.ToString(), id.ToString());
  }
}