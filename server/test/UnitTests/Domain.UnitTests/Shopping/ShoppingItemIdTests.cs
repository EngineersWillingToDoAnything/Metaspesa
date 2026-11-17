using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class ShoppingItemIdTests {
  [Fact(DisplayName = "Creates and compares shopping item IDs by value")]
  public void Create_CreatesValueObject_WhenIdIsValid() {
    var value = Guid.Parse("00000000-0000-7000-8000-000000000001");

    var id = ShoppingItemId.Create(value);

    Assert.Equal(ShoppingItemId.Create(value), id);
    Assert.Equal(value, id.Value);
  }

  [Fact(DisplayName = "Rejects empty shopping item ID")]
  public void Create_ThrowsExactException_WhenIdIsEmpty() {
    Assert.Throws<InvalidShoppingItemIdException>(() =>
      ShoppingItemId.Create(Guid.Empty));
  }

  [Fact(DisplayName = "Formats shopping item ID as its primitive value")]
  public void ToString_ReturnsGuidText() {
    var value = Guid.Parse("00000000-0000-7000-8000-000000000001");

    var id = ShoppingItemId.Create(value);

    Assert.Equal(value.ToString(), id.ToString());
  }
}