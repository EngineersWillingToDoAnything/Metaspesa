using Metaspesa.Domain.Identity;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.Shopping;

public record CreatePurchaseParams(
  Guid PurchaseId,
  Guid BuyerId,
  Guid ShoppingListId,
  DateTime PurchasedAt,
  IReadOnlyCollection<CreatePurchaseItem> Items
);

public record CreatePurchaseItem(
  Guid PriceSnapshotId,
  int Amount
);

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
    DateTime purchasedAt,
    IReadOnlyCollection<PurchaseItem> items
  ) : base() {
    Id = id;
    BuyerId = buyerId;
    ShoppingListId = shoppingListId;
    PurchasedAt = purchasedAt;
    _items = [.. items];
  }

  public static Purchase Create(CreatePurchaseParams @params) {
    ArgumentNullException.ThrowIfNull(@params);
    CreatePurchaseItem[] items = [.. @params.Items];

    EnsureValidDate(@params.PurchasedAt);
    EnsureNotEmpty(items);
    EnsureNoDuplicates(items);

    PurchaseItem[] purchaseItems = [.. items.Select(item =>
      PurchaseItem.Create(item.PriceSnapshotId, item.Amount))
    ];

    return new Purchase(
      new PurchaseId(@params.PurchaseId),
      new UserId(@params.BuyerId),
      new ShoppingListId(@params.ShoppingListId),
      @params.PurchasedAt,
      purchaseItems
    );
  }

  public static Purchase Rehydrate(
    Guid id,
    Guid? buyerId,
    Guid? shoppingListId,
    DateTime purchasedAt,
    IReadOnlyCollection<PurchaseItem> items
  ) {
    return new Purchase(
      new PurchaseId(id),
      buyerId is null ? null : new UserId(buyerId.Value),
      shoppingListId is null ? null : new ShoppingListId(shoppingListId.Value),
      purchasedAt,
      items);
  }

  private static void EnsureNotEmpty(CreatePurchaseItem[] items) {
    if (items.Length == 0) {
      throw new EmptyCheckoutException();
    }
  }

  private static void EnsureNoDuplicates(CreatePurchaseItem[] items) {
    Guid? duplicateSnapshotId = items
      .GroupBy(item => item.PriceSnapshotId)
      .Where(group => group.Count() > 1)
      .Select(group => (Guid?)group.Key)
      .FirstOrDefault();
    if (duplicateSnapshotId.HasValue) {
      throw new DuplicatePurchaseItemException(duplicateSnapshotId.Value);
    }
  }

  private static void EnsureValidDate(DateTime purchasedAt) {
    if (purchasedAt == default || purchasedAt.Kind != DateTimeKind.Utc) {
      throw new InvalidPurchasedAtException(purchasedAt);
    }
  }
}