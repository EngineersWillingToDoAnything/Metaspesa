namespace Metaspesa.Domain.Shopping.Errors;

public class EmptyCheckoutException : ShoppingDomainException {
  public EmptyCheckoutException()
    : base("Checkout.Empty", "Shopping list must contain at least one checked item to checkout") { }
  public EmptyCheckoutException(string message) : base(message) { }
  public EmptyCheckoutException(string message, Exception innerException)
    : base(message, innerException) { }
}