using System.Collections.Immutable;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Markets;

public sealed class ProductFormat {
  private readonly List<PriceSnapshot> _pendingSnapshots = [];

  public ProductFormatId Id { get; }
  public Quantity Quantity { get; }
  public ImageUrl? ImageUrl { get; }
  public PriceSnapshot LatestSnapshot { get; private set; }
  public IReadOnlyCollection<PriceSnapshot> PendingSnapshots => _pendingSnapshots.AsReadOnly();

  private ProductFormat(
    ProductFormatId id,
    Quantity quantity,
    ImageUrl? imageUrl,
    PriceSnapshot latestSnapshot
  ) {
    Id = id;
    Quantity = quantity;
    ImageUrl = imageUrl;
    LatestSnapshot = latestSnapshot;
  }

  internal static PreparedFormatObservation PrepareObservation(
    ProductFormatObservationParams observation
  ) {
    ArgumentNullException.ThrowIfNull(observation);
    return new PreparedFormatObservation(
      Quantity.Create(observation.Quantity, observation.UnitOfMeasure),
      Money.Create(observation.Price),
      new ImageUrl(observation.ImageUrl));
  }

  internal static ProductFormat Create(
    PreparedFormatObservation observation, ObservationDate observedOn
  ) {
    var id = new ProductFormatId(Uid.Create());
    var format = new ProductFormat(
      id,
      observation.Quantity,
      observation.ImageUrl,
      PriceSnapshot.Create(observation.Price, observedOn));
    format._pendingSnapshots.Add(format.LatestSnapshot);
    return format;
  }

  public static ProductFormat Rehydrate(
    ProductFormatId id,
    Quantity quantity,
    ImageUrl? imageUrl,
    PriceSnapshot latestSnapshot
  ) {
    return new ProductFormat(id, quantity, imageUrl, latestSnapshot);
  }

  public ProductFormatPrimitives ToPrimitives() {
    return new ProductFormatPrimitives(
      Id.Value,
      Quantity.Amount,
      Quantity.UnitOfMeasure.Value,
      ImageUrl?.Value.ToString(),
      LatestSnapshot.ToPrimitives(),
      [.. PendingSnapshots.Select(s => s.ToPrimitives())]);
  }

  internal bool RecordSnapshot(Money price, ObservationDate observedOn) {
    EnsureIsLaterObservation(observedOn);
    EnsureDoesNotConflictCurrentObservation(price, observedOn);

    if (LatestSnapshot.HasSamePrice(price)) {
      return false;
    }

    LatestSnapshot = PriceSnapshot.Create(price, observedOn);
    _pendingSnapshots.Add(LatestSnapshot);
    return true;
  }

  private void EnsureDoesNotConflictCurrentObservation(
    Money price, ObservationDate observedOn
  ) {
    if (LatestSnapshot.IsConflictingWith(price, observedOn)) {
      throw new ConflictingPriceObservationException(Id, observedOn);
    }
  }

  private void EnsureIsLaterObservation(ObservationDate observedOn) {
    if (LatestSnapshot.IsLaterThan(observedOn)) {
      throw new EarlierPriceObservationException(
        Id, observedOn, LatestSnapshot.ObservedOn);
    }
  }
}

internal sealed record PreparedFormatObservation(
  Quantity Quantity, Money Price, ImageUrl ImageUrl);

#pragma warning disable CA1054, CA1056 // Persistence snapshots intentionally represent URLs as primitive strings.

public sealed record ProductFormatPrimitives(
  Guid Id,
  decimal Quantity,
  string UnitOfMeasure,
  string? ImageUrl,
  PriceSnapshotPrimitives LatestSnapshot,
  ImmutableArray<PriceSnapshotPrimitives> PendingSnapshots
);

#pragma warning restore CA1054, CA1056