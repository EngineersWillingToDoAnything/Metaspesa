namespace Metaspesa.Domain.Markets.Errors;

public class ProductObservationIdentityMismatchException : MarketDomainException {
  public ProductObservationIdentityMismatchException() { }

  public ProductObservationIdentityMismatchException(ProductIdentity expected, ProductIdentity actual)
    : base("Market.ProductObservation.IdentityMismatch",
      $"Observed product identity '{actual}' does not match '{expected}'.") { }

  public ProductObservationIdentityMismatchException(string message) : base(message) { }

  public ProductObservationIdentityMismatchException(string message, Exception innerException)
    : base(message, innerException) { }
}