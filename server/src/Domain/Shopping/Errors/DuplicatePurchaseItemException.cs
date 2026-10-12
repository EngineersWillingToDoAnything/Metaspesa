using Metaspesa.Domain.Markets;

namespace Metaspesa.Domain.Shopping.Errors;

public class DuplicatePurchaseItemException : PurchaseDomainException {
  public DuplicatePurchaseItemException() { }
  public DuplicatePurchaseItemException(Guid priceSnapshotId)
    : base(
      "Purchase.Item.DuplicatePriceSnapshot",
      $"Price snapshot '{priceSnapshotId}' appears more than once.") { }
  public DuplicatePurchaseItemException(string message) : base(message) { }
  public DuplicatePurchaseItemException(string message, Exception innerException)
    : base(message, innerException) { }
}