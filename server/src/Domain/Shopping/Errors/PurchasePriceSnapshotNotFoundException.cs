using Metaspesa.Domain.Markets;

namespace Metaspesa.Domain.Shopping.Errors;

public class PurchasePriceSnapshotNotFoundException : PurchaseDomainException {
  public PurchasePriceSnapshotNotFoundException() { }
  public PurchasePriceSnapshotNotFoundException(ProductFormatId productFormatId)
    : base(
      "Purchase.PriceSnapshot.NotFound",
      $"No price snapshot exists for product format '{productFormatId}'.") { }
  public PurchasePriceSnapshotNotFoundException(string message) : base(message) { }
  public PurchasePriceSnapshotNotFoundException(
    string message, Exception innerException
  ) : base(message, innerException) { }
}