using System.Collections.Immutable;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping.Errors;
using Metaspesa.Domain.Shopping.Events;

namespace Metaspesa.Domain.Shopping;

public record AddItemsParams(Guid ProductFormatUid, int Amount, bool IsChecked);

public sealed class ShoppingList : AggregateRoot {
  private ShoppingListName? _name;
  private readonly DateTime? _deletedAt;
  private readonly List<UserId> _ownerIds;
  private readonly List<ShoppingItem> _items;

  public ShoppingListId Id { get; }

  private ShoppingList(
    ShoppingListId id,
    IEnumerable<UserId> ownerIds,
    ShoppingListName? name,
    IEnumerable<ShoppingItem>? items,
    DateTime? deletedAt
  ) : base() {
    Id = id;
    _ownerIds = [.. ownerIds];
    _name = name;
    _deletedAt = deletedAt;
    _items = items?.ToList() ?? [];
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

  public static ShoppingList Rehydrate(ShoppingListPrimitives primitives) {
    ArgumentNullException.ThrowIfNull(primitives);

    return new ShoppingList(
      new ShoppingListId(primitives.Id),
      primitives.OwnerIds.Select(o => new UserId(o)),
      primitives.Name is null ? null : new ShoppingListName(primitives.Name),
      primitives.Items.Select(ShoppingItem.Rehydrate),
      primitives.DeletedAt);
  }

  public ShoppingListPrimitives ToPrimitives() {
    ImmutableArray<Guid> ownerIds = [.. _ownerIds.Select(owner => owner.Value)];
    ImmutableArray<ShoppingItemPrimitives> items = [..
      _items.Select(item => item.ToPrimitives())
    ];

    return new ShoppingListPrimitives(
      Id.Value, _name?.Value, _name is null, _deletedAt, ownerIds, items);
  }

  public void Update(string name) {
    _name = new ShoppingListName(name);
  }

  public void AddItems(IEnumerable<AddItemsParams> items) {
    ArgumentNullException.ThrowIfNull(items);
    AddItemsParams[] additions = [.. items];
    EnsureIsNotEmpty(additions);
    EnsureNoDuplicates(additions);

    ShoppingItem[] preparedItems = [.. additions.Select(ShoppingItem.Create)];
    _items.AddRange(preparedItems);
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

  public Guid Checkout(Guid buyerId, DateTime checkoutAt) {
    EnsureBuyerCanCheckoutList(buyerId);

    var itemsToCheckout = _items.Where(item => item.CanBeCheckedOut()).ToList();
    if (itemsToCheckout.Count == 0) {
      throw new EmptyCheckoutException();
    }

    foreach (ShoppingItem item in itemsToCheckout) {
      item.Update(null, false);
    }

    CheckedOutItem[] checkedOutItems = [.. itemsToCheckout
      .Select(item => item.CreateCheckedOutItem())
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

  private void EnsureBuyerCanCheckoutList(Guid buyerId) {
    if (!_ownerIds.Any(ownerId => ownerId.Value == buyerId)) {
      throw new BuyerDoNotOwnListException(Id, buyerId);
    }
  }

  private ShoppingItem FindItem(Guid shoppingItemId) {
    return _items.FirstOrDefault(item =>
      !item.WasDeleted() && item.Id.Value == shoppingItemId) ??
    throw new ShoppingItemNotFoundException(shoppingItemId);
  }

  private static void EnsureIsNotEmpty(IEnumerable<AddItemsParams> items) {
    if (!items.Any()) {
      throw new EmptyShoppingItemsException();
    }
  }

  private void EnsureNoDuplicates(IEnumerable<AddItemsParams> items) {
    ShoppingItem[] activeItems = [.. _items.Where(item => !item.WasDeleted())];
    var formatIds = new HashSet<Guid>();

    foreach (AddItemsParams item in items) {
      bool duplicatedInBatch = !formatIds.Add(item.ProductFormatUid);
      bool alreadyInList = activeItems.Any(existing => existing.ReferencesSameFormat(item));
      if (duplicatedInBatch || alreadyInList) {
        throw new DuplicateShoppingItemException(item.ProductFormatUid);
      }
    }
  }

  public bool IsNamed(string? name) => _name?.Value == name;

  public bool IsOwnedBy(Guid ownerId) {
    return _ownerIds.Any(owner => owner.Value == ownerId);
  }

  public bool IsTemporary() => _name is null;

  public bool WasDeleted() => _deletedAt is not null;

  public int CountActiveItems() => _items.Count(item => !item.WasDeleted());

  public bool IncludesItem(AddItemsParams item) {
    return _items.Any(existing =>
      !existing.WasDeleted() && existing.MatchesItem(item));
  }

  public bool HasRemovedItem(AddItemsParams item, DateTime deletedAt) {
    return _items.Any(existing =>
      existing.WasDeletedAt(deletedAt) && existing.MatchesItem(item));
  }

  public Guid ItemIdFor(AddItemsParams item) {
    ShoppingItem shoppingItem = _items.Single(existing =>
      !existing.WasDeleted() && existing.ReferencesSameFormat(item));

    return shoppingItem.Id.Value;
  }
}

public sealed record ShoppingListPrimitives(
  Guid Id,
  string? Name,
  bool IsTemporary,
  DateTime? DeletedAt,
  ImmutableArray<Guid> OwnerIds,
  ImmutableArray<ShoppingItemPrimitives> Items
);