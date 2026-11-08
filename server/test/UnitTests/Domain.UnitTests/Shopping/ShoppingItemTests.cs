using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class ShoppingItemTests {
  [Fact]
  public void Create_AssignsItemDetails() {
    // Arrange
    var expected = new AddItemsParams(Guid.CreateVersion7(), 2, true);

    // Act
    var item = ShoppingItem.Create(expected);

    // Assert
    Assert.True(item.MatchesItem(expected));
  }

  [Fact]
  public void Create_GeneratesDistinctIdentities() {
    // Arrange
    var parameters = new AddItemsParams(Guid.CreateVersion7(), 2, true);
    var first = ShoppingItem.Create(parameters);

    // Act
    var second = ShoppingItem.Create(parameters);

    // Assert
    Assert.NotEqual(first.Id, second.Id);
  }

  [Fact]
  public void Rehydrate_PreservesState() {
    // Arrange
    var parameters = new AddItemsParams(Guid.CreateVersion7(), 3, true);
    var original = ShoppingItem.Create(parameters);
    original.Delete(new DateTime(2026, 10, 3, 8, 30, 0, DateTimeKind.Utc));

    // Act
    var restored = ShoppingItem.Rehydrate(original.ToPrimitives());

    // Assert
    Assert.Equivalent(original, restored, strict: true);
  }

  [Fact]
  public void Delete_RecordsRemovalTime() {
    // Arrange
    var parameters = new AddItemsParams(Guid.CreateVersion7(), 1, true);
    var item = ShoppingItem.Create(parameters);
    var deletedAt = new DateTime(2026, 10, 3, 8, 30, 0, DateTimeKind.Utc);

    // Act
    item.Delete(deletedAt);

    // Assert
    Assert.True(item.WasDeletedAt(deletedAt));
  }

  [Fact]
  public void Delete_RejectsAlreadyRemovedItem() {
    // Arrange
    var parameters = new AddItemsParams(Guid.CreateVersion7(), 1, true);
    var item = ShoppingItem.Create(parameters);
    var deletedAt = new DateTime(2026, 10, 3, 8, 30, 0, DateTimeKind.Utc);
    item.Delete(deletedAt);

    // Act
    Exception? failure = Record.Exception(() => item.Delete(deletedAt.AddDays(1)));

    // Assert
    Assert.IsType<ShoppingItemAlreadyDeletedException>(failure);
  }

  [Fact]
  public void Update_AssignsProvidedDetails() {
    // Arrange
    var original = new AddItemsParams(Guid.CreateVersion7(), 1, false);
    var item = ShoppingItem.Create(original);
    AddItemsParams expected = original with { Amount = 4, IsChecked = true };

    // Act
    item.Update(expected.Amount, expected.IsChecked);

    // Assert
    Assert.True(item.MatchesItem(expected));
  }
}