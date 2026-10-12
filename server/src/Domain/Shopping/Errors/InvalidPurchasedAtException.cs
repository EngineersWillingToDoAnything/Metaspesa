namespace Metaspesa.Domain.Shopping.Errors;

public class InvalidPurchasedAtException : PurchaseDomainException {
  public InvalidPurchasedAtException() { }
  public InvalidPurchasedAtException(DateTime value)
    : base(
      "Purchase.PurchasedAt.Invalid",
      $"Purchase time '{value:O}' must be a non-default UTC value.") { }
  public InvalidPurchasedAtException(string message) : base(message) { }
  public InvalidPurchasedAtException(string message, Exception innerException)
    : base(message, innerException) { }
}