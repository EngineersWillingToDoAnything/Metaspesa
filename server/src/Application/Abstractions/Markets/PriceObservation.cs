using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Application.Abstractions.Markets;

public sealed record PriceObservation {
  public ProductFormatId ProductFormatId { get; }
  public Money Price { get; }
  public ObservationTime ObservedAt { get; }

  public PriceObservation(
    ProductFormatId productFormatId, Money price, DateTime observedAt
  ) {
    ProductFormatId = productFormatId;
    Price = price;
    ObservedAt = ObservationTime.Create(observedAt);
  }
}