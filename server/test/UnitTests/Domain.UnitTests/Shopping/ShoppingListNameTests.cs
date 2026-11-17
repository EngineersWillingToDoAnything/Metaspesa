using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class ShoppingListNameTests {
  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  [InlineData(" Legacy List ")]
  public void Constructor_PreservesStoredTextWithoutValidationOrTrimming(string storedName) {
    // Act
    var name = new ShoppingListName(storedName);

    // Assert
    Assert.Equal(storedName, name.Value);
  }

  [Fact(DisplayName = "Trims and compares shopping list names by value")]
  public void Create_NormalizesName() {
    Assert.Equal(ShoppingListName.Create("Weekly"), ShoppingListName.Create("  Weekly  "));
  }

  [Theory(DisplayName = "Rejects invalid shopping list name")]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void Create_ThrowsExactException_WhenNameIsInvalid(string? value) {
    Assert.Throws<InvalidShoppingListNameException>(() => ShoppingListName.Create(value!));
  }
}