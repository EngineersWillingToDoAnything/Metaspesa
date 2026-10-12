namespace Metaspesa.Domain.Shopping.Errors;

public class EmptyPurchaseItemsException : PurchaseDomainException {
  public EmptyPurchaseItemsException()
    : base("Purchase.Items.Empty", "Purchase must contain at least one item.") { }
  public EmptyPurchaseItemsException(string message) : base(message) { }
  public EmptyPurchaseItemsException(string message, Exception innerException)
    : base(message, innerException) { }
}