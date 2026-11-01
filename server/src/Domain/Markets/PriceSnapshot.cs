using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Markets;

public sealed class PriceSnapshot {
  public PriceSnapshotId Id { get; }
  public ProductFormatId ProductFormatId { get; }
  public Money Price { get; }
  public ObservationDate ObservedOn { get; }

  public bool HasSamePrice(Money price) => Price == price;

  private PriceSnapshot(
    PriceSnapshotId id,
    ProductFormatId productFormatId,
    Money price,
    ObservationDate observedOn
  ) {
    Id = id;
    ProductFormatId = productFormatId;
    Price = price;
    ObservedOn = observedOn;
  }

  public static PriceSnapshot Create(
    ProductFormatId formatId, Money price, ObservationDate observedOn
  ) {
    var id = new PriceSnapshotId(Uid.Create());
    return new(id, formatId, price, observedOn);
  }

  public static PriceSnapshot Rehydrate(
    PriceSnapshotId id,
    ProductFormatId formatId,
    Money price,
    ObservationDate observedOn
  ) {
    return new(id, formatId, price, observedOn);
  }

  public bool IsConflictingWith(Money price, ObservationDate observedOn) =>
    WasObservedOn(observedOn) && !HasSamePrice(price);

  public bool IsLaterThan(ObservationDate observedOn) => ObservedOn > observedOn;

  private bool WasObservedOn(ObservationDate observedOn) => ObservedOn == observedOn;
}