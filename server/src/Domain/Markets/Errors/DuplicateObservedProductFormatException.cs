using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Markets.Errors;

public class DuplicateObservedProductFormatException : MarketDomainException {
  public DuplicateObservedProductFormatException() { }

  public DuplicateObservedProductFormatException(Quantity quantity)
    : base("Market.ProductObservation.DuplicateFormat",
      $"Format '{quantity.Amount} {quantity.UnitOfMeasure}' occurs more than once in the observation.") { }

  public DuplicateObservedProductFormatException(string message) : base(message) { }

  public DuplicateObservedProductFormatException(string message, Exception innerException)
    : base(message, innerException) { }
}