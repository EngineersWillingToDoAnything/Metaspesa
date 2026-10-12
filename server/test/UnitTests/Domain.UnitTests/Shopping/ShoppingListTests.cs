using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Identity.Errors;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;
using Metaspesa.Domain.Shopping.Events;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class ShoppingListTests {
  private static readonly UserId OwnerId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

  [Fact(DisplayName = "Creates list with version 7 id")]
  public void Create_CreatesList_WithV7Id() {
    var list = ShoppingList.Create(OwnerId.Value, "Weekly");

    Assert.Equal(7, list.Id.Value.Version);
  }

  [Fact(DisplayName = "Creates list with given name")]
  public void Create_CreatesList_WithGivenName() {
    var list = ShoppingList.Create(OwnerId.Value, "Weekly");

    Assert.Equal(new ShoppingListName("Weekly"), list.Name);
  }

  [Fact(DisplayName = "Creates list with single owner")]
  public void Create_CreatesList_WithSingleOwner() {
    var list = ShoppingList.Create(OwnerId.Value, "Weekly");

    Assert.Equal(OwnerId, Assert.Single(list.OwnerIds));
  }

  [Fact(DisplayName = "Creates list without items")]
  public void Create_CreatesList_WithoutItems() {
    var list = ShoppingList.Create(OwnerId.Value, "Weekly");

    Assert.Empty(list.Items);
  }

  [Fact(DisplayName = "Creates list without deleted timestamp")]
  public void Create_CreatesList_WithoutDeletedTimestamp() {
    var list = ShoppingList.Create(OwnerId.Value, "Weekly");

    Assert.Null(list.DeletedAt);
  }

  [Fact(DisplayName = "Rejects invalid primitive owner ID")]
  public void Create_ThrowsExactException_WhenPrimitiveOwnerIdIsInvalid() {
    Assert.Throws<InvalidUserIdException>(() =>
      ShoppingList.Create(Guid.Empty, "Weekly"));
  }

  [Fact(DisplayName = "Rehydrates persisted list state")]
  public void Rehydrate_ExposesPersistedState() {
    var deletedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    var list = ShoppingList.Rehydrate(
      new ShoppingListId(Guid.Parse("00000000-0000-7000-8000-000000000004")),
      [OwnerId],
      new ShoppingListName("Weekly"),
      deletedAt,
      [new ShoppingItem(new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000008")), new PositiveAmount(2), true)]);

    Assert.Equal(new ShoppingListId(Guid.Parse("00000000-0000-7000-8000-000000000004")), list.Id);
    Assert.Equal(new ShoppingListName("Weekly"), list.Name);
    Assert.False(list.IsTemporary);
    Assert.Equal(deletedAt, list.DeletedAt);
    Assert.Single(list.Items);
  }

  [Fact(DisplayName = "Rejects missing owner")]
  public void Rehydrate_ThrowsExactException_WhenOwnerIsMissing() {
    Assert.Throws<MissingShoppingListOwnerException>(() => ShoppingList.Rehydrate(
      new ShoppingListId(Guid.Parse("00000000-0000-7000-8000-000000000001")), [], null, null, []));
  }

  [Fact(DisplayName = "Rejects duplicate owners")]
  public void Rehydrate_ThrowsExactException_WhenOwnerIsDuplicated() {
    Assert.Throws<DuplicateShoppingListOwnerException>(() => ShoppingList.Rehydrate(
      new ShoppingListId(Guid.Parse("00000000-0000-7000-8000-000000000001")), [OwnerId, OwnerId], null, null, []));
  }

  [Fact(DisplayName = "Rejects duplicate persisted items")]
  public void Rehydrate_ThrowsExactException_WhenItemIsDuplicated() {
    var duplicateId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000002"));

    Assert.Throws<DuplicateShoppingItemException>(() => ShoppingList.Rehydrate(
      new ShoppingListId(Guid.Parse("00000000-0000-7000-8000-000000000001")),
      [OwnerId],
      null,
      null,
      [
        new ShoppingItem(duplicateId, new PositiveAmount(1), false),
        new ShoppingItem(duplicateId, new PositiveAmount(2), true),
      ]));
  }

  [Fact(DisplayName = "Allows active replacement for deleted item history")]
  public void AddItems_AddsActiveReplacement_WhenPreviousItemIsDeleted() {
    var formatId = Guid.Parse("00000000-0000-7000-8000-000000000002");
    var deletedItem = ShoppingItem.Rehydrate(
      Guid.Parse("00000000-0000-7000-8000-000000000003"),
      formatId,
      1,
      false,
      new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc));
    var list = ShoppingList.Rehydrate(
      new ShoppingListId(
        Guid.Parse("00000000-0000-7000-8000-000000000001")),
      [OwnerId],
      new ShoppingListName("Weekly"),
      null,
      [deletedItem]);

    list.AddItems([new AddItemsParams(formatId, 2, true)]);

    Assert.Single(list.Items, item => item.DeletedAt is not null);
    Assert.Single(list.Items, item => item.DeletedAt is null);
  }

  [Fact(DisplayName = "Renames temporary list")]
  public void Rename_SetsNameAndClearsTemporaryState() {
    var list = ShoppingList.Create(OwnerId, null);

    list.Update("Weekly");

    Assert.Equal(new ShoppingListName("Weekly"), list.Name);
  }

  [Fact(DisplayName = "Adds item")]
  public void AddItem_AddsTypedItem() {
    var list = ShoppingList.Create(OwnerId, null);

    list.AddItem(new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000002")), new PositiveAmount(3), true);

    ShoppingItem item = Assert.Single(list.Items);
    Assert.Equal(new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000002")), item.ProductFormatId);
    Assert.Equal(3, item.Amount.Value);
    Assert.True(item.IsChecked);
  }

  [Fact(DisplayName = "Adds primitive item batch")]
  public void AddItems_AddsPrimitiveItems() {
    var list = ShoppingList.Create(OwnerId.Value, "Test");
    var firstFormatId = Guid.Parse("00000000-0000-7000-8000-000000000002");
    var secondFormatId = Guid.Parse("00000000-0000-7000-8000-000000000003");

    list.AddItems([
      new AddItemsParams(firstFormatId, 2, true),
      new AddItemsParams(secondFormatId, 3, false),
    ]);

    var actual = list.Items.Select(item => new {
      ProductFormatId = item.ProductFormatId.Value,
      Amount = item.Amount.Value,
      item.IsChecked,
    });
    var expected = new[] {
      new { ProductFormatId = firstFormatId, Amount = 2, IsChecked = true },
      new { ProductFormatId = secondFormatId, Amount = 3, IsChecked = false },
    };
    Assert.Equal(expected, actual);
  }

  [Fact(DisplayName = "Rejects empty primitive item batch")]
  public void AddItems_ThrowsExactException_WhenPrimitiveBatchIsEmpty() {
    var list = ShoppingList.Create(OwnerId.Value, "Test");

    void action() => list.AddItems(Array.Empty<AddItemsParams>());

    Assert.Throws<EmptyShoppingItemsException>(action);
  }

  [Fact(DisplayName = "Rejects duplicate primitive batch atomically")]
  public void AddItems_ThrowsWhenPrimitiveBatchHasDuplicate() {
    var list = ShoppingList.Create(OwnerId, null);
    var duplicateId = Guid.Parse("00000000-0000-7000-8000-000000000002");
    IEnumerable<AddItemsParams> items = [
      new AddItemsParams(duplicateId, 1, false),
      new AddItemsParams(duplicateId, 2, true),
    ];

    void action() => list.AddItems(items);

    Assert.Throws<DuplicateShoppingItemException>(action);
  }

  [Fact(DisplayName = "Rejects existing primitive item")]
  public void AddItems_ThrowsWhenPrimitiveItemExists() {
    var list = ShoppingList.Create(OwnerId, null);
    var existingId = Guid.Parse("00000000-0000-7000-8000-000000000002");
    list.AddItems([new AddItemsParams(existingId, 1, false)]);

    void action() => list.AddItems([new AddItemsParams(existingId, 2, true)]);

    Assert.Throws<DuplicateShoppingItemException>(action);
  }

  [Fact(DisplayName = "Rejects invalid primitive item batch atomically")]
  public void AddItems_ThrowsWhenPrimitiveItemIsInvalid() {
    var list = ShoppingList.Create(OwnerId, null);

    void action() => list.AddItems([new AddItemsParams(Guid.Empty, 1, false)]);

    Assert.Throws<InvalidProductFormatIdException>(action);
  }

  [Fact(DisplayName = "Updates amount and checked state atomically")]
  public void UpdateItem_UpdatesProvidedFields() {
    var list = ShoppingList.Create(OwnerId.Value, "Test list");
    list.AddItems([new AddItemsParams(Guid.CreateVersion7(), 1, false)]);
    ShoppingItem item = list.Items.Single();

    list.UpdateItem(item.Id.Value, 4, true);

    Assert.Equal(4, item.Amount.Value);
    Assert.True(item.IsChecked);
  }

  [Fact(DisplayName = "Rejects update without fields")]
  public void UpdateItem_ThrowsExactException_WhenNoFieldsProvided() {
    var list = ShoppingList.Create(OwnerId.Value, "Test list");
    list.AddItems([new AddItemsParams(Guid.CreateVersion7(), 1, false)]);
    ShoppingItem item = list.Items.Single();

    void action() => list.UpdateItem(item.Id.Value, null, null);

    Assert.Throws<EmptyShoppingItemUpdateException>(action);
  }

  [Fact(DisplayName = "Rejects update for missing item")]
  public void UpdateItem_ThrowsExactException_WhenItemIsMissing() {
    var list = ShoppingList.Create(OwnerId.Value, "Test list");

    Assert.Throws<ShoppingItemNotFoundException>(() =>
      list.UpdateItem(
        Guid.Parse("00000000-0000-7000-8000-000000000002"), 1, null));
  }

  [Fact(DisplayName = "Preserves amount when updating checked state")]
  public void UpdateItem_PreservesAmount_WhenAmountIsMissing() {
    var list = ShoppingList.Create(OwnerId.Value, "Test list");
    list.AddItems([new AddItemsParams(Guid.CreateVersion7(), 3, false)]);
    ShoppingItem item = list.Items.Single();

    list.UpdateItem(item.Id.Value, null, true);

    Assert.Equal(3, list.Items.Single().Amount.Value);
  }

  [Fact(DisplayName = "Preserves checked state when updating amount")]
  public void UpdateItem_PreservesCheckedState_WhenCheckedStateIsMissing() {
    var list = ShoppingList.Create(OwnerId.Value, "Test list");
    list.AddItems([new AddItemsParams(Guid.CreateVersion7(), 3, true)]);
    ShoppingItem item = list.Items.Single();

    list.UpdateItem(item.Id.Value, 3, null);

    Assert.True(item.IsChecked);
  }

  [Fact(DisplayName = "Marks existing item deleted at supplied time")]
  public void RemoveItem_MarksItemDeletedAtSuppliedTime() {
    var list = ShoppingList.Create(OwnerId.Value, "Test list");
    list.AddItems([new AddItemsParams(Guid.CreateVersion7(), 3, true)]);
    ShoppingItem item = list.Items.Single();

    var deletedAt = new DateTime(2026, 10, 3, 8, 30, 0, DateTimeKind.Utc);

    list.RemoveItem(item.Id.Value, deletedAt);

    Assert.Equal(deletedAt, item.DeletedAt);
  }

  [Fact(DisplayName = "Rejects removal of missing item")]
  public void RemoveItem_ThrowsExactException_WhenItemIsMissing() {
    var list = ShoppingList.Create(OwnerId.Value, "Test list");

    void action() => list.RemoveItem(Guid.CreateVersion7(), DateTime.UtcNow);

    Assert.Throws<ShoppingItemNotFoundException>(action);
  }

  [Fact(DisplayName = "Returns checked items without mutation")]
  public void CheckedItems_ReturnsOnlyCheckedItems() {
    var list = ShoppingList.Create(OwnerId, null);
    list.AddItem(new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000002")), new PositiveAmount(1), true);
    list.AddItem(new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000003")), new PositiveAmount(1), false);

    IReadOnlyCollection<ShoppingItem> checkedItems = list.CheckedItems();

    Assert.Single(checkedItems);
    Assert.Equal(2, list.Items.Count);
  }

  [Fact(DisplayName = "Resets checked items")]
  public void ResetCheckedItems_UnchecksAllItems() {
    var list = ShoppingList.Create(OwnerId, null);
    list.AddItem(new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000002")), new PositiveAmount(1), true);

    list.ResetCheckedItems(OwnerId.Value,
      new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc));

    Assert.False(list.Items.Single().IsChecked);
  }

  [Fact(DisplayName = "Preserves items when resetting checked state")]
  public void ResetCheckedItems_DoesNotRemoveItems() {
    var list = ShoppingList.Create(OwnerId, null);
    list.AddItem(new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000002")), new PositiveAmount(1), true);

    list.ResetCheckedItems(OwnerId.Value,
      new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc));

    Assert.Single(list.Items);
  }

  [Fact]
  public void ResetCheckedItems_RecordsShoppingListId() {
    var list = ShoppingList.Create(OwnerId, null);
    var buyerId = Guid.Parse("11111111-1111-1111-1111-111111111112");
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    list.AddItems([new AddItemsParams(
      Guid.Parse("00000000-0000-7000-8000-000000000002"), 2, true)]);

    list.ResetCheckedItems(buyerId, checkoutAt);

    var domainEvent = (ShoppingListCheckedOut)list.DomainEvents.Single();
    Assert.Equal(list.Id.Value, domainEvent.ShoppingListId);
  }

  [Fact]
  public void ResetCheckedItems_RecordsBuyerId() {
    var list = ShoppingList.Create(OwnerId, null);
    var buyerId = Guid.Parse("11111111-1111-1111-1111-111111111112");
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    list.AddItems([new AddItemsParams(
      Guid.Parse("00000000-0000-7000-8000-000000000002"), 2, true)]);

    list.ResetCheckedItems(buyerId, checkoutAt);

    var domainEvent = (ShoppingListCheckedOut)list.DomainEvents.Single();
    Assert.Equal(buyerId, domainEvent.BuyerId);
  }

  [Fact]
  public void ResetCheckedItems_RecordsCheckoutAt() {
    var list = ShoppingList.Create(OwnerId, null);
    var buyerId = Guid.Parse("11111111-1111-1111-1111-111111111112");
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    list.AddItems([new AddItemsParams(
      Guid.Parse("00000000-0000-7000-8000-000000000002"), 2, true)]);

    list.ResetCheckedItems(buyerId, checkoutAt);

    var domainEvent = (ShoppingListCheckedOut)list.DomainEvents.Single();
    Assert.Equal(checkoutAt, domainEvent.CheckoutAt);
  }

  [Fact]
  public void ResetCheckedItems_RecordsActiveCheckedProductFormats() {
    var list = ShoppingList.Create(OwnerId, null);
    var firstId = Guid.Parse("00000000-0000-7000-8000-000000000002");
    var secondId = Guid.Parse("00000000-0000-7000-8000-000000000003");
    var uncheckedId = Guid.Parse("00000000-0000-7000-8000-000000000004");
    var deletedId = Guid.Parse("00000000-0000-7000-8000-000000000005");
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    list.AddItems([
      new AddItemsParams(firstId, 2, true),
      new AddItemsParams(secondId, 3, true),
      new AddItemsParams(uncheckedId, 4, false),
      new AddItemsParams(deletedId, 5, true),
    ]);
    list.RemoveItem(list.Items.Single(item =>
      item.ProductFormatId.Value == deletedId).Id.Value, checkoutAt);

    list.ResetCheckedItems(OwnerId.Value, checkoutAt);

    var domainEvent = (ShoppingListCheckedOut)list.DomainEvents.Single();
    Assert.Equal([firstId, secondId],
      domainEvent.Items.Select(item => item.ProductFormatId));
  }

  [Fact]
  public void ResetCheckedItems_RecordsCheckedAmounts() {
    var list = ShoppingList.Create(OwnerId, null);
    var firstId = Guid.Parse("00000000-0000-7000-8000-000000000002");
    var secondId = Guid.Parse("00000000-0000-7000-8000-000000000003");
    var uncheckedId = Guid.Parse("00000000-0000-7000-8000-000000000004");
    var deletedId = Guid.Parse("00000000-0000-7000-8000-000000000005");
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    list.AddItems([
      new AddItemsParams(firstId, 2, true),
      new AddItemsParams(secondId, 3, true),
      new AddItemsParams(uncheckedId, 4, false),
      new AddItemsParams(deletedId, 5, true),
    ]);
    list.RemoveItem(list.Items.Single(item =>
      item.ProductFormatId.Value == deletedId).Id.Value, checkoutAt);

    list.ResetCheckedItems(OwnerId.Value, checkoutAt);

    var domainEvent = (ShoppingListCheckedOut)list.DomainEvents.Single();
    Assert.Equal([2, 3], domainEvent.Items.Select(item => item.Amount));
  }

  [Fact]
  public void ResetCheckedItems_ExcludesUncheckedItemsFromEvent() {
    var list = ShoppingList.Create(OwnerId, null);
    var firstId = Guid.Parse("00000000-0000-7000-8000-000000000002");
    var secondId = Guid.Parse("00000000-0000-7000-8000-000000000003");
    var uncheckedId = Guid.Parse("00000000-0000-7000-8000-000000000004");
    var deletedId = Guid.Parse("00000000-0000-7000-8000-000000000005");
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    list.AddItems([
      new AddItemsParams(firstId, 2, true),
      new AddItemsParams(secondId, 3, true),
      new AddItemsParams(uncheckedId, 4, false),
      new AddItemsParams(deletedId, 5, true),
    ]);
    list.RemoveItem(list.Items.Single(item =>
      item.ProductFormatId.Value == deletedId).Id.Value, checkoutAt);

    list.ResetCheckedItems(OwnerId.Value, checkoutAt);

    var domainEvent = (ShoppingListCheckedOut)list.DomainEvents.Single();
    Assert.DoesNotContain(domainEvent.Items,
      item => item.ProductFormatId == uncheckedId);
  }

  [Fact]
  public void ResetCheckedItems_ExcludesDeletedItemsFromEvent() {
    var list = ShoppingList.Create(OwnerId, null);
    var firstId = Guid.Parse("00000000-0000-7000-8000-000000000002");
    var secondId = Guid.Parse("00000000-0000-7000-8000-000000000003");
    var uncheckedId = Guid.Parse("00000000-0000-7000-8000-000000000004");
    var deletedId = Guid.Parse("00000000-0000-7000-8000-000000000005");
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    list.AddItems([
      new AddItemsParams(firstId, 2, true),
      new AddItemsParams(secondId, 3, true),
      new AddItemsParams(uncheckedId, 4, false),
      new AddItemsParams(deletedId, 5, true),
    ]);
    list.RemoveItem(list.Items.Single(item =>
      item.ProductFormatId.Value == deletedId).Id.Value, checkoutAt);

    list.ResetCheckedItems(OwnerId.Value, checkoutAt);

    var domainEvent = (ShoppingListCheckedOut)list.DomainEvents.Single();
    Assert.DoesNotContain(domainEvent.Items,
      item => item.ProductFormatId == deletedId);
  }

  [Fact]
  public void ResetCheckedItems_PreservesDeletedItemState() {
    var list = ShoppingList.Create(OwnerId, null);
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    list.AddItems([new AddItemsParams(
      Guid.Parse("00000000-0000-7000-8000-000000000002"), 2, true)]);
    ShoppingItem deletedItem = list.Items.Single();
    list.RemoveItem(deletedItem.Id.Value, checkoutAt);
    list.AddItems([new AddItemsParams(
      Guid.Parse("00000000-0000-7000-8000-000000000003"), 1, true)]);

    list.ResetCheckedItems(OwnerId.Value, checkoutAt);

    Assert.True(deletedItem.IsChecked);
  }

  [Fact]
  public void ResetCheckedItems_EventLinesSurviveSubsequentItemChanges() {
    var list = ShoppingList.Create(OwnerId, null);
    var formatId = Guid.Parse("00000000-0000-7000-8000-000000000002");
    list.AddItems([new AddItemsParams(formatId, 2, true)]);
    list.ResetCheckedItems(OwnerId.Value,
      new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc));
    var domainEvent = (ShoppingListCheckedOut)list.DomainEvents.Single();

    list.UpdateItem(list.Items.Single().Id.Value, 99, true);

    Assert.Equal([new CheckedOutItem(formatId, 2)], domainEvent.Items);
  }

  [Fact]
  public void ResetCheckedItems_GeneratesDistinctCheckoutIdsForEachOccurrence() {
    var list = ShoppingList.Create(OwnerId, null);
    var checkoutAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    list.AddItems([new AddItemsParams(
      Guid.Parse("00000000-0000-7000-8000-000000000002"), 2, true)]);
    list.ResetCheckedItems(OwnerId.Value, checkoutAt);
    list.UpdateItem(list.Items.Single().Id.Value, null, true);

    list.ResetCheckedItems(OwnerId.Value, checkoutAt.AddMinutes(1));

    Guid[] checkoutIds = list.DomainEvents.Cast<ShoppingListCheckedOut>()
      .Select(domainEvent => domainEvent.CheckoutId).ToArray();
    Assert.Equal(2, checkoutIds.Distinct().Count());
  }

  [Fact]
  public void Rehydrate_DoesNotRaiseEventsForPersistedItems() {
    var list = ShoppingList.Rehydrate(
      new ShoppingListId(Guid.Parse("00000000-0000-7000-8000-000000000001")),
      [OwnerId], null, null,
      [new ShoppingItem(
        new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000002")),
        new PositiveAmount(2), true)]);

    Assert.Empty(list.DomainEvents);
  }
}