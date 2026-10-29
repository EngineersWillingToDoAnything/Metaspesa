namespace Metaspesa.Domain.Markets.Errors;

public class EarlierPriceObservationException : MarketDomainException {
  public EarlierPriceObservationException() { }

  public EarlierPriceObservationException(ProductFormatId formatId, ObservationTime observedAt, ObservationTime latestObservedAt)
    : base("Market.ProductObservation.EarlierPrice",
      $"Observation '{observedAt.Value:O}' for format '{formatId}' predates its latest snapshot '{latestObservedAt.Value:O}'.") { }

  public EarlierPriceObservationException(string message) : base(message) { }

  public EarlierPriceObservationException(string message, Exception innerException)
    : base(message, innerException) { }
}