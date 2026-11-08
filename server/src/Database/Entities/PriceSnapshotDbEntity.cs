using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Database.Entities;

internal class PriceSnapshotDbEntity {
  public Guid Id { get; set; }
  public Guid ProductFormatId { get; set; }
  public decimal PriceAmount { get; set; }
  public string CurrencyCode { get; set; } = "EUR";
  public DateOnly ObservedOn { get; set; }

  public ProductFormatDbEntity ProductFormat { get; set; } = null!;
  public ICollection<PurchaseItemDbEntity> PurchaseItems { get; set; } = [];

  internal PriceSnapshotPrimitives ToPrimitives() {
    return new PriceSnapshotPrimitives(Id, PriceAmount, ObservedOn);
  }
}