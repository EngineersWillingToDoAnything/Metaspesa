using Metaspesa.Database.Entities;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.Database.UnitTests;

public static class ShoppingItemDbEntityTest {
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public static void ToPrimitives_MapsStoredItemDetails(bool removed) {
    // Arrange
    var entity = new ShoppingItemDbEntity {
      Id = Guid.CreateVersion7(), ShoppingListId = Guid.CreateVersion7(),
      ProductFormatId = Guid.CreateVersion7(), Amount = 3, IsChecked = true,
      DeletedAt = removed ? new DateTime(2026, 10, 3, 8, 30, 0, DateTimeKind.Utc) : null,
    };
    var expected = new ShoppingItemPrimitives(
      entity.Id, entity.ProductFormatId, entity.Amount, entity.IsChecked, entity.DeletedAt);

    // Act
    ShoppingItemPrimitives actual = entity.ToPrimitives();

    // Assert
    Assert.Equal(expected, actual);
  }
}