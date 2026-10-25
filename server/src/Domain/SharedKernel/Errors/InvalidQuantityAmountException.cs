namespace Metaspesa.Domain.SharedKernel.Errors;

public class InvalidQuantityAmountException : DomainException {
  public InvalidQuantityAmountException() { }

  public InvalidQuantityAmountException(string message) : base(message) { }

  public InvalidQuantityAmountException(string message, Exception innerException)
    : base(message, innerException) { }

  public InvalidQuantityAmountException(decimal amount)
    : base($"Quantity amount must be positive, at most 9999999.999, and have at most three fractional digits. Amount: {amount}") {
    Amount = amount;
  }

  public decimal Amount { get; }
}