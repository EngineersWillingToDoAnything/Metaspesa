using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class PurchaseItemIdTests {
  [Fact]
  public void Constructor_PreservesIdentity() {
    var value = Guid.Parse("00000000-0000-7000-8000-000000000001");

    var id = new PurchaseItemId(value);

    Assert.Equal(value, id.Value);
  }

  [Fact]
  public void Constructor_RejectsEmptyId() {
    Assert.Throws<InvalidPurchaseItemIdException>(() =>
      new PurchaseItemId(Guid.Empty));
  }

  [Fact]
  public void Equals_UsesIdentityValue() {
    var value = Guid.Parse("00000000-0000-7000-8000-000000000001");

    Assert.Equal(new PurchaseItemId(value), new PurchaseItemId(value));
  }

  [Fact]
  public void ToString_ReturnsPrimitiveIdentity() {
    var value = Guid.Parse("00000000-0000-7000-8000-000000000001");
    var id = new PurchaseItemId(value);

    Assert.Equal(value.ToString(), id.ToString());
  }
}