using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;
using Metaspesa.Domain.Shopping.Events;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class ShoppingListTests {
  [Fact(DisplayName = "Creates list with given name")]
  public void Create_CreatesList_WithGivenName() {
    // Act
    var list = ShoppingList.Create(Guid.CreateVersion7(), "Weekly");

    // Assert
    Assert.True(list.IsNamed("Weekly"));
  }

  [Fact(DisplayName = "Creates list with single owner")]
  public void Create_CreatesList_WithSingleOwner() {
    // Arrange
    var ownerId = Guid.CreateVersion7();

    // Act
    var list = ShoppingList.Create(ownerId, "Weekly");

    // Assert
    Assert.True(list.IsOwnedBy(ownerId));
  }

  [Fact(DisplayName = "Creates list without items")]
  public void Create_CreatesList_WithoutItems() {
    // Act
    var list = ShoppingList.Create(Guid.CreateVersion7(), "Weekly");

    // Assert
    Assert.Equal(0, list.CountActiveItems());
  }

  [Fact(DisplayName = "Creates list without deleted timestamp")]
  public void Create_CreatesList_WithoutDeletedTimestamp() {
    // Act
    var list = ShoppingList.Create(Guid.CreateVersion7(), "Weekly");

    // Assert
    Assert.False(list.WasDeleted());
  }

  [Fact(DisplayName = "Rehydrates persisted list state")]
  public void Rehydrate_PreservesState() {
    // Arrange
    var original = ShoppingList.Create(Guid.CreateVersion7(), "Weekly");
    var item = new AddItemsParams(Guid.CreateVersion7(), 2, true);
    original.AddItems([item]);

    // Act
    var restored = ShoppingList.Rehydrate(original.ToPrimitives());

    // Assert
    Assert.Equivalent(original, restored, strict: true);
  }

  [Fact(DisplayName = "Allows active replacement for deleted item history")]
  public void AddItems_AddsActiveReplacement_WhenPreviousItemIsDeleted() {
    // Arrange
    var list = ShoppingList.Create(Guid.CreateVersion7(), "Weekly");
    var original = new AddItemsParams(Guid.CreateVersion7(), 1, false);
    var removedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    list.AddItems([original]);
    list.RemoveItem(list.ItemIdFor(original), removedAt);
    AddItemsParams replacement = original with { Amount = 2, IsChecked = true };

    // Act
    list.AddItems([replacement]);

    // Assert
    Assert.True(list.IncludesItem(replacement));
  }

  [Fact(DisplayName = "Renames temporary list")]
  public void Rename_SetsNameAndClearsTemporaryState() {
    // Arrange
    var list = ShoppingList.Create(Guid.CreateVersion7(), null);

    // Act
    list.Update("Weekly");

    // Assert
    Assert.True(list.IsNamed("Weekly"));
  }

  [Fact(DisplayName = "Adds primitive item batch")]
  public void AddItems_AddsPrimitiveItems() {
    // Arrange
    var list = ShoppingList.Create(Guid.CreateVersion7(), "Test");
    AddItemsParams[] items = [
      new(Guid.CreateVersion7(), 2, true),
      new(Guid.CreateVersion7(), 3, false),
    ];

    // Act
    list.AddItems(items);

    // Assert
    Assert.All(items, item => Assert.True(list.IncludesItem(item)));
  }

  [Fact(DisplayName = "Rejects empty primitive item batch")]
  public void AddItems_ThrowsExactException_WhenPrimitiveBatchIsEmpty() {
    // Arrange
    var list = ShoppingList.Create(Guid.CreateVersion7(), "Test");

    // Act
    void action() => list.AddItems([]);

    // Assert
    Assert.Throws<EmptyShoppingItemsException>(action);
  }

  [Fact(DisplayName = "Rejects duplicate primitive batch atomically")]
  public void AddItems_ThrowsWhenPrimitiveBatchHasDuplicate() {
    // Arrange
    var list = ShoppingList.Create(Guid.CreateVersion7(), null);
    var duplicateId = Guid.CreateVersion7();
    IEnumerable<AddItemsParams> items = [
      new AddItemsParams(duplicateId, 1, false),
      new AddItemsParams(duplicateId, 2, true),
    ];

    // Act
    void action() => list.AddItems(items);

    // Assert
    Assert.Throws<DuplicateShoppingItemException>(action);
  }

  [Fact(DisplayName = "Rejects existing primitive item")]
  public void AddItems_ThrowsWhenPrimitiveItemExists() {
    // Arrange
    var list = ShoppingList.Create(Guid.CreateVersion7(), null);
    var existingId = Guid.CreateVersion7();
    list.AddItems([new AddItemsParams(existingId, 1, false)]);

    // Act
    void action() => list.AddItems([new AddItemsParams(existingId, 2, true)]);

    // Assert
    Assert.Throws<DuplicateShoppingItemException>(action);
  }

  [Fact(DisplayName = "Updates amount and checked state atomically")]
  public void UpdateItem_UpdatesProvidedFields() {
    // Arrange
    var list = ShoppingList.Create(Guid.CreateVersion7(), "Weekly");
    var original = new AddItemsParams(Guid.CreateVersion7(), 1, false);
    list.AddItems([original]);
    Guid itemId = list.ItemIdFor(original);
    AddItemsParams expected = original with { Amount = 4, IsChecked = true };

    // Act
    list.UpdateItem(itemId, 4, true);

    // Assert
    Assert.True(list.IncludesItem(expected));
  }

  [Fact(DisplayName = "Rejects update for missing item")]
  public void UpdateItem_ThrowsExactException_WhenItemIsMissing() {
    // Arrange
    var list = ShoppingList.Create(Guid.CreateVersion7(), "Test list");

    // Act
    void action() => list.UpdateItem(Guid.CreateVersion7(), 1, null);

    // Assert
    Assert.Throws<ShoppingItemNotFoundException>(action);
  }

  [Fact(DisplayName = "Marks existing item deleted at supplied time")]
  public void RemoveItem_MarksItemDeletedAtSuppliedTime() {
    // Arrange
    var list = ShoppingList.Create(Guid.CreateVersion7(), "Weekly");
    var item = new AddItemsParams(Guid.CreateVersion7(), 3, true);
    var deletedAt = new DateTime(2026, 10, 3, 8, 30, 0, DateTimeKind.Utc);
    list.AddItems([item]);
    Guid itemId = list.ItemIdFor(item);

    // Act
    list.RemoveItem(itemId, deletedAt);

    // Assert
    Assert.True(list.HasRemovedItem(item, deletedAt));
  }

  [Fact(DisplayName = "Rejects removal of missing item")]
  public void RemoveItem_ThrowsExactException_WhenItemIsMissing() {
    // Arrange
    var list = ShoppingList.Create(Guid.CreateVersion7(), "Test list");

    // Act
    void action() => list.RemoveItem(Guid.CreateVersion7(), DateTime.UtcNow);

    // Assert
    Assert.Throws<ShoppingItemNotFoundException>(action);
  }

  [Fact]
  public void Checkout_RejectsEmptyList() {
    // Arrange
    var buyerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(buyerId, null);
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    // Assert
    Assert.Throws<EmptyCheckoutException>(() =>
      list.Checkout(buyerId, checkoutAt));
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, true)]
  public void Checkout_RejectsListWithoutEligibleItems(
    bool isChecked, bool isDeleted
  ) {
    // Arrange
    var buyerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(buyerId, null);
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    var item = new AddItemsParams(Guid.CreateVersion7(), 2, isChecked);
    list.AddItems([item]);
    Guid itemId = list.ItemIdFor(item);
    if (isDeleted) {
      list.RemoveItem(itemId, checkoutAt.AddDays(-1));
    }

    // Assert
    Assert.Throws<EmptyCheckoutException>(() =>
      list.Checkout(buyerId, checkoutAt));
  }

  [Fact]
  public void Checkout_PreservesUncheckedItemState_WhenOtherItemIsChecked() {
    // Arrange
    var buyerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(buyerId, null);
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    var uncheckedItem = new AddItemsParams(Guid.CreateVersion7(), 2, false);
    list.AddItems([
      uncheckedItem,
      new AddItemsParams(
        Guid.CreateVersion7(), 1, true),
    ]);

    // Act
    list.Checkout(buyerId, checkoutAt);

    // Assert
    Assert.True(list.IncludesItem(uncheckedItem));
  }

  [Fact(DisplayName = "Resets checked items")]
  public void Checkout_UnchecksAllItems() {
    // Arrange
    var buyerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(buyerId, null);
    var item = new AddItemsParams(Guid.CreateVersion7(), 1, true);
    list.AddItems([item]);

    // Act
    list.Checkout(buyerId,
      new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc));

    // Assert
    Assert.True(list.IncludesItem(item with { IsChecked = false }));
  }

  [Fact(DisplayName = "Preserves items when resetting checked state")]
  public void Checkout_DoesNotRemoveItems() {
    // Arrange
    var buyerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(buyerId, null);
    var item = new AddItemsParams(Guid.CreateVersion7(), 1, true);
    list.AddItems([item]);

    // Act
    list.Checkout(buyerId,
      new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc));

    // Assert
    Assert.Equal(1, list.CountActiveItems());
  }

  [Fact]
  public void Checkout_RecordsShoppingListId() {
    // Arrange
    var buyerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(buyerId, "Test");
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    list.AddItems([new AddItemsParams(
      Guid.CreateVersion7(), 2, true)]);

    // Act
    list.Checkout(buyerId, checkoutAt);

    // Assert
    var domainEvent = (ShoppingListCheckedOut)list.DomainEvents.Single();
    Assert.Equal(list.Id.Value, domainEvent.ShoppingListId);
  }

  [Fact]
  public void Checkout_RecordsBuyerId() {
    // Arrange
    var buyerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(buyerId, "Test");
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    list.AddItems([new AddItemsParams(
      Guid.CreateVersion7(), 2, true)]);

    // Act
    list.Checkout(buyerId, checkoutAt);

    // Assert
    var domainEvent = (ShoppingListCheckedOut)list.DomainEvents.Single();
    Assert.Equal(buyerId, domainEvent.BuyerId);
  }

  [Fact]
  public void Checkout_RecordsCheckoutAt() {
    // Arrange
    var buyerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(buyerId, null);
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    list.AddItems([new AddItemsParams(
      Guid.CreateVersion7(), 2, true)]);

    // Act
    list.Checkout(buyerId, checkoutAt);

    // Assert
    var domainEvent = (ShoppingListCheckedOut)list.DomainEvents.Single();
    Assert.Equal(checkoutAt, domainEvent.CheckoutAt);
  }

  [Fact]
  public void Checkout_RecordsActiveCheckedProductFormats() {
    // Arrange
    var buyerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(buyerId, null);
    var firstId = Guid.CreateVersion7();
    var secondId = Guid.CreateVersion7();
    var uncheckedId = Guid.CreateVersion7();
    var deletedItem = new AddItemsParams(Guid.CreateVersion7(), 5, true);
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    list.AddItems([
      new AddItemsParams(firstId, 2, true),
      new AddItemsParams(secondId, 3, true),
      new AddItemsParams(uncheckedId, 4, false),
      deletedItem,
    ]);
    list.RemoveItem(list.ItemIdFor(deletedItem), checkoutAt);

    // Act
    list.Checkout(buyerId, checkoutAt);

    // Assert
    var domainEvent = (ShoppingListCheckedOut)list.DomainEvents.Single();
    Assert.Equal([firstId, secondId],
      domainEvent.Items.Select(item => item.ProductFormatId));
  }

  [Fact]
  public void Checkout_RecordsCheckedAmounts() {
    // Arrange
    var buyerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(buyerId, null);
    var firstId = Guid.CreateVersion7();
    var secondId = Guid.CreateVersion7();
    var uncheckedId = Guid.CreateVersion7();
    var deletedItem = new AddItemsParams(Guid.CreateVersion7(), 5, true);
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    list.AddItems([
      new AddItemsParams(firstId, 2, true),
      new AddItemsParams(secondId, 3, true),
      new AddItemsParams(uncheckedId, 4, false),
      deletedItem,
    ]);
    list.RemoveItem(list.ItemIdFor(deletedItem), checkoutAt);

    // Act
    list.Checkout(buyerId, checkoutAt);

    // Assert
    var domainEvent = (ShoppingListCheckedOut)list.DomainEvents.Single();
    Assert.Equal([2, 3], domainEvent.Items.Select(item => item.Amount));
  }

  [Fact]
  public void Checkout_ExcludesUncheckedItemsFromEvent() {
    // Arrange
    var buyerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(buyerId, null);
    var firstId = Guid.CreateVersion7();
    var secondId = Guid.CreateVersion7();
    var uncheckedId = Guid.CreateVersion7();
    var deletedItem = new AddItemsParams(Guid.CreateVersion7(), 5, true);
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    list.AddItems([
      new AddItemsParams(firstId, 2, true),
      new AddItemsParams(secondId, 3, true),
      new AddItemsParams(uncheckedId, 4, false),
      deletedItem,
    ]);
    list.RemoveItem(list.ItemIdFor(deletedItem), checkoutAt);

    // Act
    list.Checkout(buyerId, checkoutAt);

    // Assert
    var domainEvent = (ShoppingListCheckedOut)list.DomainEvents.Single();
    Assert.DoesNotContain(domainEvent.Items,
      item => item.ProductFormatId == uncheckedId);
  }

  [Fact]
  public void Checkout_ExcludesDeletedItemsFromEvent() {
    // Arrange
    var buyerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(buyerId, null);
    var firstId = Guid.CreateVersion7();
    var secondId = Guid.CreateVersion7();
    var uncheckedId = Guid.CreateVersion7();
    var deletedItem = new AddItemsParams(Guid.CreateVersion7(), 5, true);
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    list.AddItems([
      new AddItemsParams(firstId, 2, true),
      new AddItemsParams(secondId, 3, true),
      new AddItemsParams(uncheckedId, 4, false),
      deletedItem,
    ]);
    list.RemoveItem(list.ItemIdFor(deletedItem), checkoutAt);

    // Act
    list.Checkout(buyerId, checkoutAt);

    // Assert
    var domainEvent = (ShoppingListCheckedOut)list.DomainEvents.Single();
    Assert.DoesNotContain(domainEvent.Items,
      item => item.ProductFormatId == deletedItem.ProductFormatUid);
  }

  [Fact]
  public void Checkout_PreservesDeletedItemState() {
    // Arrange
    var buyerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(buyerId, null);
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    var removedItem = new AddItemsParams(Guid.CreateVersion7(), 2, true);
    list.AddItems([removedItem]);
    list.RemoveItem(list.ItemIdFor(removedItem), checkoutAt);
    list.AddItems([new AddItemsParams(Guid.CreateVersion7(), 1, true)]);

    // Act
    list.Checkout(buyerId, checkoutAt);

    // Assert
    Assert.True(list.HasRemovedItem(removedItem, checkoutAt));
  }

  [Fact]
  public void Checkout_EventLinesSurviveSubsequentItemChanges() {
    // Arrange
    var buyerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(buyerId, null);
    var formatId = Guid.CreateVersion7();
    var item = new AddItemsParams(formatId, 2, true);
    list.AddItems([item]);
    list.Checkout(buyerId,
      new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc));
    var domainEvent = (ShoppingListCheckedOut)list.DomainEvents.Single();

    // Act
    list.UpdateItem(list.ItemIdFor(item), 99, true);

    // Assert
    Assert.Equal([new CheckedOutItem(formatId, 2)], domainEvent.Items);
  }

  [Fact]
  public void Checkout_GeneratesDistinctCheckoutIdsForEachOccurrence() {
    // Arrange
    var buyerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(buyerId, null);
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    var item = new AddItemsParams(Guid.CreateVersion7(), 2, true);
    list.AddItems([item]);
    list.Checkout(buyerId, checkoutAt);
    list.UpdateItem(list.ItemIdFor(item), null, true);

    // Act
    list.Checkout(buyerId, checkoutAt.AddMinutes(1));

    // Assert
    Guid[] checkoutIds = [.. list.DomainEvents
      .Cast<ShoppingListCheckedOut>()
      .Select(domainEvent => domainEvent.CheckoutId)
    ];
    Assert.Equal(2, checkoutIds.Distinct().Count());
  }

  [Fact]
  public void Rehydrate_DoesNotRaiseEventsForPersistedItems() {
    // Arrange
    var buyerId = Guid.CreateVersion7();
    var original = ShoppingList.Create(buyerId, "Weekly");
    original.AddItems([new AddItemsParams(Guid.CreateVersion7(), 2, true)]);
    original.Checkout(
      buyerId, new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc));

    // Act
    var restored = ShoppingList.Rehydrate(original.ToPrimitives());

    // Assert
    Assert.Empty(restored.DomainEvents);
  }

  [Fact]
  public void ToPrimitives_DoesNotConsumePendingDomainEvents() {
    // Arrange
    var ownerId = Guid.CreateVersion7();
    var formatId = Guid.CreateVersion7();
    var checkoutAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    var list = ShoppingList.Create(ownerId, "Weekly");
    list.AddItems([new AddItemsParams(formatId, 1, true)]);
    list.Checkout(ownerId, checkoutAt);

    // Act
    _ = list.ToPrimitives();

    // Assert
    Assert.Single(list.DomainEvents);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" ")]
  public void Create_CreatesTemporaryList_WhenNameIsBlank(string? name) {
    // Arrange
    var ownerId = Guid.CreateVersion7();

    // Act
    var list = ShoppingList.Create(ownerId, name);

    // Assert
    Assert.True(list.IsTemporary());
  }

  [Fact]
  public void Update_MakesTemporaryListPermanent() {
    // Arrange
    var list = ShoppingList.Create(Guid.CreateVersion7(), null);

    // Act
    list.Update("Weekly");

    // Assert
    Assert.False(list.IsTemporary());
  }

  [Fact]
  public void AddItems_PreservesRemovedHistory_WhenReplacementIsAdded() {
    // Arrange
    var list = ShoppingList.Create(Guid.CreateVersion7(), "Weekly");
    var original = new AddItemsParams(Guid.CreateVersion7(), 1, true);
    var removedAt = new DateTime(2026, 10, 3, 8, 30, 0, DateTimeKind.Utc);
    list.AddItems([original]);
    list.RemoveItem(list.ItemIdFor(original), removedAt);
    AddItemsParams replacement = original with { Amount = 2 };

    // Act
    list.AddItems([replacement]);

    // Assert
    Assert.True(list.HasRemovedItem(original, removedAt));
  }

  [Fact]
  public void AddItems_DoesNotAddItems_WhenBatchContainsDuplicates() {
    // Arrange
    var list = ShoppingList.Create(Guid.CreateVersion7(), "Weekly");
    var item = new AddItemsParams(Guid.CreateVersion7(), 1, false);
    AddItemsParams[] additions = [item, item with { Amount = 2 }];

    // Act
    _ = Record.Exception(() => list.AddItems(additions));

    // Assert
    Assert.Equal(0, list.CountActiveItems());
  }

  [Fact]
  public void AddItems_DoesNotAddEarlierItems_WhenLaterFormatIsInvalid() {
    // Arrange
    var list = ShoppingList.Create(Guid.CreateVersion7(), "Weekly");
    AddItemsParams[] additions = [
      new(Guid.CreateVersion7(), 1, false),
      new(Guid.Empty, 2, true),
    ];

    // Act
    _ = Record.Exception(() => list.AddItems(additions));

    // Assert
    Assert.Equal(0, list.CountActiveItems());
  }

  [Fact]
  public void AddItems_DoesNotAddEarlierItems_WhenLaterAmountIsInvalid() {
    // Arrange
    var list = ShoppingList.Create(Guid.CreateVersion7(), "Weekly");
    AddItemsParams[] additions = [
      new(Guid.CreateVersion7(), 1, false),
      new(Guid.CreateVersion7(), 0, true),
    ];

    // Act
    _ = Record.Exception(() => list.AddItems(additions));

    // Assert
    Assert.Equal(0, list.CountActiveItems());
  }

  [Fact]
  public void UpdateItem_RejectsRemovedItem() {
    // Arrange
    var list = ShoppingList.Create(Guid.CreateVersion7(), "Weekly");
    var item = new AddItemsParams(Guid.CreateVersion7(), 2, true);
    list.AddItems([item]);
    Guid itemId = list.ItemIdFor(item);
    list.RemoveItem(itemId, new DateTime(2026, 10, 3, 8, 30, 0, DateTimeKind.Utc));

    // Act
    Exception? failure = Record.Exception(() => list.UpdateItem(itemId, 4, false));

    // Assert
    Assert.IsType<ShoppingItemNotFoundException>(failure);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void Checkout_RejectsBuyerWhoDoesNotOwnList(bool includesCheckedItem) {
    // Arrange
    var list = ShoppingList.Create(Guid.CreateVersion7(), "Weekly");
    var buyerId = Guid.CreateVersion7();
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    if (includesCheckedItem) {
      list.AddItems([new AddItemsParams(Guid.CreateVersion7(), 2, true)]);
    }

    // Act
    void NewMethod() => list.Checkout(buyerId, checkoutAt);

    // Assert
    Assert.Throws<BuyerDoNotOwnListException>(NewMethod);
  }
}