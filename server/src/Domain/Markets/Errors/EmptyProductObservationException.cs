namespace Metaspesa.Domain.Markets.Errors;

public class EmptyProductObservationException : MarketDomainException {
  public EmptyProductObservationException()
    : base("Market.ProductObservation.Empty", "A product observation must contain at least one format.") { }

  public EmptyProductObservationException(string message) : base(message) { }

  public EmptyProductObservationException(string message, Exception innerException)
    : base(message, innerException) { }
}