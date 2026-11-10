using Metaspesa.Domain.Shopping;

namespace Metaspesa.Database.Entities;

internal class PurchaseItemDbEntity {
  public Guid Id { get; set; }
  public Guid PurchaseId { get; set; }
  public Guid PriceSnapshotId { get; set; }
  public int Amount { get; set; } = 1;

  public PurchaseDbEntity Purchase { get; set; } = null!;
  public PriceSnapshotDbEntity PriceSnapshot { get; set; } = null!;

  public PurchaseItemPrimitives ToPrimitives() {
    return new PurchaseItemPrimitives(Id, PriceSnapshotId, Amount);
  }
}