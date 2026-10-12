using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Shopping;

public sealed class PurchaseItem {
  public PurchaseItemId Id { get; }
  public PriceSnapshotId PriceSnapshotId { get; }
  public PositiveAmount Amount { get; }

  private PurchaseItem(
    PurchaseItemId id,
    PriceSnapshotId priceSnapshotId,
    PositiveAmount amount
  ) {
    Id = id;
    Amount = amount;
    PriceSnapshotId = priceSnapshotId;
  }

  public static PurchaseItem Create(
    Guid priceSnapshotId,
    int amount
  ) {
    return new PurchaseItem(
      new PurchaseItemId(Uid.Create()),
      new PriceSnapshotId(priceSnapshotId),
      new PositiveAmount(amount)
    );
  }

  public static PurchaseItem Rehydrate(
    Guid id,
    Guid priceSnapshotId,
    int amount
  ) {
    return new PurchaseItem(
      new PurchaseItemId(id),
      new PriceSnapshotId(priceSnapshotId),
      new PositiveAmount(amount)
    );
  }
}