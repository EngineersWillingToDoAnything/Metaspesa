using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Markets;

public sealed class PriceSnapshot {
  public PriceSnapshotId Id { get; }
  public ProductFormatId ProductFormatId { get; }
  public Money Price { get; }
  public ObservationTime ObservedAt { get; }

  public bool HasSamePrice(Money price) => Price == price;

  private PriceSnapshot(
    PriceSnapshotId id,
    ProductFormatId productFormatId,
    Money price,
    ObservationTime observedAt
  ) {
    Id = id;
    ProductFormatId = productFormatId;
    Price = price;
    ObservedAt = observedAt;
  }

  public static PriceSnapshot Create(
    ProductFormatId formatId, Money price, ObservationTime observedAt
  ) {
    var id = new PriceSnapshotId(Uid.Create());
    return new(id, formatId, price, observedAt);
  }

  public static PriceSnapshot Rehydrate(
    PriceSnapshotId id,
    ProductFormatId formatId,
    Money price,
    ObservationTime observedAt
  ) {
    return new(id, formatId, price, observedAt);
  }

  public bool IsConflictingWith(Money price, ObservationTime observedAt) =>
    WasObservedAt(observedAt) && !HasSamePrice(price);

  public bool IsLaterThan(ObservationTime observedAt) => ObservedAt > observedAt;

  private bool WasObservedAt(ObservationTime observedAt) => ObservedAt == observedAt;
}