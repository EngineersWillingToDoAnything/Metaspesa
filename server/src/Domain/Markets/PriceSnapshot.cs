using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Markets;

public sealed class PriceSnapshot {
  private readonly Money _price;
  private readonly ObservationDate _observedOn;

  public PriceSnapshotId Id { get; }

  private PriceSnapshot(
    PriceSnapshotId id,
    Money price,
    ObservationDate observedOn
  ) {
    Id = id;
    _price = price;
    _observedOn = observedOn;
  }

  internal static PriceSnapshot Create(
    Money price, ObservationDate observedOn
  ) {
    var id = new PriceSnapshotId(Uid.Create());
    return new(id, price, observedOn);
  }

  internal static PriceSnapshot Rehydrate(
    PriceSnapshotPrimitives primitives
  ) {
    return new PriceSnapshot(
      new PriceSnapshotId(primitives.Id),
      new Money(primitives.Price),
      new ObservationDate(primitives.ObservedOn));
  }

  public PriceSnapshotPrimitives ToPrimitives() {
    return new PriceSnapshotPrimitives(
      Id.Value, _price.Amount, _observedOn.Value);
  }

  public bool HasSamePrice(Money price) => _price == price;

  internal bool MatchesObservation(decimal price, DateOnly observedOn) =>
    _price.Amount == price && _observedOn.Value == observedOn;

  public bool IsConflictingWith(Money price, ObservationDate observedOn) =>
    WasObservedOn(observedOn) && !HasSamePrice(price);

  public bool IsLaterThan(ObservationDate observedOn) => _observedOn > observedOn;

  private bool WasObservedOn(ObservationDate observedOn) => _observedOn == observedOn;
}

public sealed record PriceSnapshotPrimitives(Guid Id, decimal Price, DateOnly ObservedOn);