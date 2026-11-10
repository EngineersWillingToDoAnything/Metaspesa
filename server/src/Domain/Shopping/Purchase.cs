using System.Collections.Immutable;
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
  private readonly UserId? _buyerId;
  private readonly ShoppingListId? _shoppingListId;
  private readonly PurchaseTime _purchasedAt;
  private readonly List<PurchaseItem> _items;

  public PurchaseId Id { get; }

  private Purchase(
    PurchaseId id,
    UserId? buyerId,
    ShoppingListId? shoppingListId,
    PurchaseTime purchasedAt,
    IReadOnlyCollection<PurchaseItem> items
  ) : base() {
    Id = id;
    _buyerId = buyerId;
    _shoppingListId = shoppingListId;
    _purchasedAt = purchasedAt;
    _items = [.. items];
  }

  public static Purchase Create(CreatePurchaseParams parameters) {
    ArgumentNullException.ThrowIfNull(parameters);
    CreatePurchaseItem[] items = [.. parameters.Items];

    EnsureNotEmpty(items);
    EnsureNoDuplicates(items);

    PurchaseItem[] purchaseItems = [.. items.Select(item =>
      PurchaseItem.Create(item.PriceSnapshotId, item.Amount))
    ];

    return new Purchase(
      new PurchaseId(parameters.PurchaseId),
      new UserId(parameters.BuyerId),
      new ShoppingListId(parameters.ShoppingListId),
      PurchaseTime.Create(parameters.PurchasedAt),
      purchaseItems
    );
  }

  public static Purchase Rehydrate(PurchasePrimitives primitives) {
    ArgumentNullException.ThrowIfNull(primitives);

    return new Purchase(
      new PurchaseId(primitives.Id),
      primitives.BuyerId is null ? null : new UserId(primitives.BuyerId.Value),
      primitives.ShoppingListId is null ? null : new ShoppingListId(primitives.ShoppingListId.Value),
      new PurchaseTime(primitives.PurchasedAt),
      [.. primitives.Items.Select(PurchaseItem.Rehydrate)]);
  }

  public PurchasePrimitives ToPrimitives() {
    ImmutableArray<PurchaseItemPrimitives> items = [..
      _items.Select(item => item.ToPrimitives())
    ];

    return new PurchasePrimitives(
      Id.Value, _buyerId?.Value, _shoppingListId?.Value, _purchasedAt.Value, items);
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

  public bool WasMadeBy(Guid buyerId) => _buyerId?.Value == buyerId;
  public bool WasMadeAt(DateTime purchaseAt) => _purchasedAt.Value == purchaseAt;
  public bool WasForShoppingList(Guid shoppingListId) =>
    _shoppingListId?.Value == shoppingListId;
  public bool IncludesItem(Guid priceSnapshotId, int amount) {
    return _items.Any(item => item.MatchesPurchase(priceSnapshotId, amount));
  }
}

public sealed record PurchasePrimitives(
  Guid Id,
  Guid? BuyerId,
  Guid? ShoppingListId,
  DateTime PurchasedAt,
  ImmutableArray<PurchaseItemPrimitives> Items
);