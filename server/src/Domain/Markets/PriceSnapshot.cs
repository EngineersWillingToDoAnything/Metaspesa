using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Markets;

public sealed class PriceSnapshot {
  public PriceSnapshotId Id { get; }
  public Money Price { get; }
  public ObservationDate ObservedOn { get; }

  public bool HasSamePrice(Money price) => Price == price;

  private PriceSnapshot(
    PriceSnapshotId id,
    Money price,
    ObservationDate observedOn
  ) {
    Id = id;
    Price = price;
    ObservedOn = observedOn;
  }

  internal static PriceSnapshot Create(
    Money price, ObservationDate observedOn
  ) {
    var id = new PriceSnapshotId(Uid.Create());
    return new(id, price, observedOn);
  }

  public static PriceSnapshot Rehydrate(
    PriceSnapshotId id,
    Money price,
    ObservationDate observedOn
  ) {
    return new(id, price, observedOn);
  }

  public PriceSnapshotPrimitives ToPrimitives() {
    return new PriceSnapshotPrimitives(Id.Value, Price.Amount, ObservedOn.Value);
  }

  public bool IsConflictingWith(Money price, ObservationDate observedOn) =>
    WasObservedOn(observedOn) && !HasSamePrice(price);

  public bool IsLaterThan(ObservationDate observedOn) => ObservedOn > observedOn;

  private bool WasObservedOn(ObservationDate observedOn) => ObservedOn == observedOn;
}

public sealed record PriceSnapshotPrimitives(Guid Id, decimal Price, DateOnly ObservedOn);