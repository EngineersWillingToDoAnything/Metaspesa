namespace Metaspesa.Domain.Shopping.Errors;

public class InvalidPurchaseIdException : PurchaseDomainException {
  public InvalidPurchaseIdException() { }
  public InvalidPurchaseIdException(Guid value)
    : base("Purchase.Id.Invalid", $"Purchase ID '{value}' is invalid.") { }
  public InvalidPurchaseIdException(string message) : base(message) { }
  public InvalidPurchaseIdException(string message, Exception innerException)
    : base(message, innerException) { }
}