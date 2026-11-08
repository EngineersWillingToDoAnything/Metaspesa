using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Shopping;

public sealed class PurchaseItem {
  private readonly PriceSnapshotId _priceSnapshotId;
  private readonly PositiveAmount _amount;

  public PurchaseItemId Id { get; }

  private PurchaseItem(
    PurchaseItemId id,
    PriceSnapshotId priceSnapshotId,
    PositiveAmount amount
  ) {
    Id = id;
    _amount = amount;
    _priceSnapshotId = priceSnapshotId;
  }

  internal static PurchaseItem Create(
    Guid priceSnapshotId,
    int amount
  ) {
    return new PurchaseItem(
      new PurchaseItemId(Uid.Create()),
      new PriceSnapshotId(priceSnapshotId),
      new PositiveAmount(amount)
    );
  }

  internal static PurchaseItem Rehydrate(
    PurchaseItemPrimitives primitives
  ) {
    return new PurchaseItem(
      new PurchaseItemId(primitives.Id),
      new PriceSnapshotId(primitives.PriceSnapshotId),
      new PositiveAmount(primitives.Amount)
    );
  }

  public PurchaseItemPrimitives ToPrimitives() {
    return new PurchaseItemPrimitives(
      Id.Value, _priceSnapshotId.Value, _amount.Value);
  }

  internal bool MatchesPurchase(Guid priceSnapshotId, int amount) {
    return _priceSnapshotId.Value == priceSnapshotId &&
      _amount.Value == amount;
  }
}

public sealed record PurchaseItemPrimitives(Guid Id, Guid PriceSnapshotId, int Amount);