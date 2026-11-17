using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class ShoppingListIdTests {
  [Fact(DisplayName = "Creates and compares shopping list IDs by value")]
  public void Create_CreatesValueObject_WhenIdIsPositive() {
    var rawId = Guid.Parse("00000000-0000-7000-8000-000000000001");
    var firstId = ShoppingListId.Create(rawId);
    var secondId = ShoppingListId.Create(rawId);

    Assert.Equal(firstId, secondId);
  }

  [Fact(DisplayName = "Rejects invalid shopping list ID")]
  public void Create_ThrowsExactException_WhenIdIsInvalid() {
    Assert.Throws<InvalidShoppingListIdException>(() =>
      ShoppingListId.Create(Guid.Empty));
  }
}