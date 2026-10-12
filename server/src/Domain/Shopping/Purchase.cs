using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.Shopping;

public sealed class Purchase : AggregateRoot {
  private readonly List<PurchaseItem> _items;

  public PurchaseId Id { get; }
  public UserId? BuyerId { get; }
  public ShoppingListId? ShoppingListId { get; }
  public DateTime PurchasedAt { get; }
  public IReadOnlyCollection<PurchaseItem> Items => _items.AsReadOnly();

  private Purchase(
    PurchaseId id,
    UserId? buyerId,
    ShoppingListId? shoppingListId,
    IEnumerable<PurchaseItem> items,
    DateTime purchasedAt
  ) : base() {
    ArgumentNullException.ThrowIfNull(items);
    if (purchasedAt == default || purchasedAt.Kind != DateTimeKind.Utc) {
      throw new InvalidPurchasedAtException(purchasedAt);
    }

    _items = [.. items];
    if (_items.Count == 0) {
      throw new EmptyPurchaseItemsException();
    }

    PriceSnapshotId? duplicateSnapshotId = _items
      .GroupBy(item => item.PriceSnapshotId)
      .Where(group => group.Count() > 1)
      .Select(group => (PriceSnapshotId?)group.Key)
      .FirstOrDefault();
    if (duplicateSnapshotId.HasValue) {
      throw new DuplicatePurchaseItemException(duplicateSnapshotId.Value);
    }

    Id = id;
    BuyerId = buyerId;
    ShoppingListId = shoppingListId;
    PurchasedAt = purchasedAt;
  }

  public static Purchase Create(
    UserId? buyerId,
    ShoppingListId? shoppingListId,
    IEnumerable<PurchaseItem> items,
    DateTime purchasedAt
  ) {
    var id = new PurchaseId(Uid.Create());

    return new(id, buyerId, shoppingListId, items, purchasedAt);
  }

  public static Purchase Rehydrate(
    PurchaseId id,
    UserId? buyerId,
    ShoppingListId? shoppingListId,
    IEnumerable<PurchaseItem> items,
    DateTime purchasedAt
  ) => new(id, buyerId, shoppingListId, items, purchasedAt);
}