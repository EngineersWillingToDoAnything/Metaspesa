using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping.Errors;
using Metaspesa.Domain.Shopping.Events;

namespace Metaspesa.Domain.Shopping;

public record AddItemsParams(Guid ProductFormatUid, int Amount, bool IsChecked);

public sealed class ShoppingList : AggregateRoot {
  private readonly List<UserId> _ownerIds;
  private readonly List<ShoppingItem> _items;

  public ShoppingListId Id { get; }
  public ShoppingListName? Name { get; private set; }
  public DateTime? DeletedAt { get; }
  public IReadOnlyCollection<UserId> OwnerIds => _ownerIds.AsReadOnly();
  public IReadOnlyCollection<ShoppingItem> Items => _items.AsReadOnly();

  public bool IsTemporary => Name is null;

  private ShoppingList(
    ShoppingListId id,
    IEnumerable<UserId> ownerIds,
    ShoppingListName? name,
    IEnumerable<ShoppingItem>? items,
    DateTime? deletedAt
  ) : base() {
    _ownerIds = [.. ownerIds];
    if (_ownerIds.Count == 0) {
      throw new MissingShoppingListOwnerException();
    }

    UserId? duplicateOwner = _ownerIds
      .GroupBy(ownerId => ownerId)
      .Where(group => group.Count() > 1)
      .Select(group => (UserId?)group.Key)
      .FirstOrDefault();
    if (duplicateOwner.HasValue) {
      throw new DuplicateShoppingListOwnerException(duplicateOwner.Value);
    }

    Id = id;
    Name = name;
    DeletedAt = deletedAt;
    _items = items?.ToList() ?? [];

    EnsureNoDuplicates(_items);
  }

  public static ShoppingList Create(
    Guid primitiveOwnerId,
    string? primitiveName
  ) {
    var id = new ShoppingListId(Uid.Create());
    var ownerId = new UserId(primitiveOwnerId);
    ShoppingListName? name = string.IsNullOrWhiteSpace(primitiveName)
      ? null : new ShoppingListName(primitiveName);

    return new ShoppingList(id, [ownerId], name, [], null);
  }

  public static ShoppingList Create(UserId ownerId, ShoppingListName? name) =>
    Create(ownerId.Value, name?.Value);

  public static ShoppingList Rehydrate(
    ShoppingListId id,
    IEnumerable<UserId> ownerIds,
    ShoppingListName? name,
    DateTime? deletedAt,
    IEnumerable<ShoppingItem> items
  ) => new(id, ownerIds, name, items, deletedAt);

  public void Update(string name) {
    Name = new ShoppingListName(name);
  }

  public void AddItem(
    ProductFormatId productFormatId, PositiveAmount amount, bool isChecked
  ) => AddItems([new AddItemsParams(productFormatId.Value, amount.Value, isChecked)]);

  public void AddItems(IEnumerable<AddItemsParams> items) {
    var additions = items.Select(i => ShoppingItem.Create(
        i.ProductFormatUid, i.Amount, i.IsChecked))
      .ToList();
    if (additions.Count == 0) {
      throw new EmptyShoppingItemsException();
    }

    EnsureNoDuplicates([.. _items, .. additions]);

    _items.AddRange(additions);
  }

  public void UpdateItem(
    Guid shoppingItemId, int? amount, bool? isChecked
  ) {
    ShoppingItem item = FindItem(shoppingItemId);
    item.Update(amount, isChecked);
  }

  public void RemoveItem(Guid shoppingItemId, DateTime deletedAt) {
    ShoppingItem item = FindItem(shoppingItemId);

    item.Delete(deletedAt);
  }

  public IReadOnlyCollection<ShoppingItem> CheckedItems() =>
    _items.Where(item => item.DeletedAt is null && item.IsChecked)
      .ToList().AsReadOnly();

  public Guid Checkout(Guid buyerId, DateTime checkoutAt) {
    var itemsToCheckout = _items.Where(item => item.CanBeCheckedOut()).ToList();
    if (itemsToCheckout.Count == 0) {
      throw new EmptyCheckoutException();
    }

    foreach (ShoppingItem item in itemsToCheckout) {
      item.Update(null, false);
    }

    CheckedOutItem[] checkedOutItems = [.. itemsToCheckout.Select(item =>
      new CheckedOutItem(item.ProductFormatId.Value, item.Amount.Value))
    ];
    var checkedOutEvent = new ShoppingListCheckedOut(
      Uid.Create(),
      Id.Value,
      buyerId,
      checkoutAt,
      checkedOutItems);
    Record(checkedOutEvent);
    return checkedOutEvent.CheckoutId;
  }

  private ShoppingItem FindItem(Guid shoppingItemId) =>
    _items.FirstOrDefault(item =>
      item.DeletedAt is null && item.Id.Value == shoppingItemId) ??
    throw new ShoppingItemNotFoundException(shoppingItemId);

  private static void EnsureNoDuplicates(IEnumerable<ShoppingItem> items) {
    ProductFormatId? duplicateItem = items
      .Where(item => item.DeletedAt is null)
      .GroupBy(item => item.ProductFormatId)
      .Where(group => group.Count() > 1)
      .Select(group => (ProductFormatId?)group.Key)
      .FirstOrDefault();

    if (duplicateItem.HasValue) {
      throw new DuplicateShoppingItemException(duplicateItem.Value);
    }
  }
}