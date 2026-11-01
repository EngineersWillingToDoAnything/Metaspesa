namespace Metaspesa.Domain.Markets.Errors;

public class EarlierPriceObservationException : MarketDomainException {
  public EarlierPriceObservationException() { }

  public EarlierPriceObservationException(
    ProductFormatId formatId,
    ObservationDate observedOn,
    ObservationDate latestObservedOn
  ) : base("Market.ProductObservation.EarlierPrice",
    $"Observation '{observedOn.Value:O}' for format '{formatId}' predates its latest snapshot '{latestObservedOn.Value:O}'.") { }

  public EarlierPriceObservationException(string message) : base(message) { }

  public EarlierPriceObservationException(string message, Exception innerException)
    : base(message, innerException) { }
}