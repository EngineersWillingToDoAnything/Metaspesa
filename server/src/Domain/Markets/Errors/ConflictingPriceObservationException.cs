namespace Metaspesa.Domain.Markets.Errors;

public class ConflictingPriceObservationException : MarketDomainException {
  public ConflictingPriceObservationException() { }

  public ConflictingPriceObservationException(
    ProductFormatId formatId, ObservationDate observedOn
  ) : base("Market.ProductObservation.ConflictingPrice",
    $"Format '{formatId}' already has a different price at '{observedOn.Value:O}'.") { }

  public ConflictingPriceObservationException(string message) : base(message) { }

  public ConflictingPriceObservationException(string message, Exception innerException)
    : base(message, innerException) { }
}