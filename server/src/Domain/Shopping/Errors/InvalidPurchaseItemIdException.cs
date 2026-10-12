namespace Metaspesa.Domain.Shopping.Errors;

public class InvalidPurchaseItemIdException : PurchaseDomainException {
  public InvalidPurchaseItemIdException() { }
  public InvalidPurchaseItemIdException(Guid value)
    : base("Purchase.Item.Id.Invalid", $"Purchase item ID '{value}' is invalid.") { }
  public InvalidPurchaseItemIdException(string message) : base(message) { }
  public InvalidPurchaseItemIdException(string message, Exception innerException)
    : base(message, innerException) { }
}