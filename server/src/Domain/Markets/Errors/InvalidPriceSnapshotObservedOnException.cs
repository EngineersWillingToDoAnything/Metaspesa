namespace Metaspesa.Domain.Markets.Errors;

public class InvalidPriceSnapshotObservedOnException : MarketDomainException {
  public InvalidPriceSnapshotObservedOnException() { }

  public InvalidPriceSnapshotObservedOnException(DateOnly value)
    : base(
      "Market.PriceSnapshot.ObservedOn.Invalid",
      $"Price snapshot observation date '{value:yyyy-MM-dd}' must be a non-default value.") { }

  public InvalidPriceSnapshotObservedOnException(string message) : base(message) { }

  public InvalidPriceSnapshotObservedOnException(
    string message, Exception innerException
  ) : base(message, innerException) { }
}