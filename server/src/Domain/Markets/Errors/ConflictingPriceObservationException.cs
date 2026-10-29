namespace Metaspesa.Domain.Markets.Errors;

public class ConflictingPriceObservationException : MarketDomainException {
  public ConflictingPriceObservationException() { }

  public ConflictingPriceObservationException(ProductFormatId formatId, ObservationTime observedAt)
    : base("Market.ProductObservation.ConflictingPrice",
      $"Format '{formatId}' already has a different price at '{observedAt.Value:O}'.") { }

  public ConflictingPriceObservationException(string message) : base(message) { }

  public ConflictingPriceObservationException(string message, Exception innerException)
    : base(message, innerException) { }
}