using Metaspesa.Database.Entities;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.Database.UnitTests;

public static class ShoppingListDbEntityTest {
  [Theory]
  [InlineData(null)]
  [InlineData("Weekly")]
  public static void ToAggregate_MapsListOwnershipAndItems(string? name) {
    // Arrange
    var expected = ShoppingList.Create(Guid.CreateVersion7(), name);
    var active = new AddItemsParams(Guid.CreateVersion7(), 2, true);
    var removed = new AddItemsParams(Guid.CreateVersion7(), 3, false);
    expected.AddItems([active, removed]);
    expected.RemoveItem(expected.ItemIdFor(removed),
      new DateTime(2026, 10, 3, 8, 30, 0, DateTimeKind.Utc));
    ShoppingListPrimitives persisted = expected.ToPrimitives();
    var entity = new ShoppingListDbEntity {
      Id = persisted.Id,
      Name = persisted.Name,
      IsTemporary = persisted.IsTemporary,
      DeletedAt = persisted.DeletedAt,
      Ownerships = [.. persisted.OwnerIds.Select(ownerId => new ShoppingListOwnershipDbEntity {
        ShoppingListId = persisted.Id, UserUid = ownerId,
      })],
      Items = [.. persisted.Items.Select(item => new ShoppingItemDbEntity {
        Id = item.Id,
        ShoppingListId = persisted.Id,
        ProductFormatId = item.ProductFormatId,
        Amount = item.Amount,
        IsChecked = item.IsChecked,
        DeletedAt = item.DeletedAt,
      })],
    };

    // Act
    ShoppingList actual = entity.ToAggregate();

    // Assert
    Assert.Equivalent(expected, actual, strict: true);
  }
}