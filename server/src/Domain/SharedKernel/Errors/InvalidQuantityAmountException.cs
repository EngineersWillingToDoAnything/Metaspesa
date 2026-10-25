namespace Metaspesa.Domain.SharedKernel.Errors;

public class InvalidQuantityAmountException : DomainException {
  public InvalidQuantityAmountException() { }

  public InvalidQuantityAmountException(string message) : base(message) { }

  public InvalidQuantityAmountException(string message, Exception innerException)
    : base(message, innerException) { }

  public InvalidQuantityAmountException(decimal amount)
    : base($"Quantity amount must be greater than zero. Amount: {amount}") {
    Amount = amount;
  }

  public decimal Amount { get; }
}