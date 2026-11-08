using System.Collections.Immutable;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Markets;

public sealed class ProductFormat {
  private readonly ImageUrl? _imageUrl;
  private PriceSnapshot _latestSnapshot;
  private readonly List<PriceSnapshot> _pendingSnapshots = [];

  public ProductFormatId Id { get; }
  public Quantity Quantity { get; }

  private ProductFormat(
    ProductFormatId id,
    Quantity quantity,
    ImageUrl? imageUrl,
    PriceSnapshot latestSnapshot
  ) {
    Id = id;
    Quantity = quantity;
    _imageUrl = imageUrl;
    _latestSnapshot = latestSnapshot;
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
    format._pendingSnapshots.Add(format._latestSnapshot);
    return format;
  }

  internal static ProductFormat Rehydrate(
    ProductFormatPrimitives primitives
  ) {
    var quantity = new Quantity(
      primitives.Quantity,
      new UnitOfMeasure(primitives.UnitOfMeasure));
    ImageUrl? imageUrl = primitives.ImageUrl is null
      ? null
      : new ImageUrl(new Uri(primitives.ImageUrl, UriKind.Absolute));

    return new ProductFormat(
      new ProductFormatId(primitives.Id),
      quantity,
      imageUrl,
      PriceSnapshot.Rehydrate(primitives.LatestSnapshot));
  }

  public ProductFormatPrimitives ToPrimitives() {
    return new ProductFormatPrimitives(
      Id.Value,
      Quantity.Amount,
      Quantity.UnitOfMeasure.Value,
      _imageUrl?.Value.ToString(),
      _latestSnapshot.ToPrimitives(),
      [.. _pendingSnapshots.Select(s => s.ToPrimitives())]);
  }

  internal bool RecordSnapshot(Money price, ObservationDate observedOn) {
    EnsureIsLaterObservation(observedOn);
    EnsureDoesNotConflictCurrentObservation(price, observedOn);

    if (_latestSnapshot.HasSamePrice(price)) {
      return false;
    }

    _latestSnapshot = PriceSnapshot.Create(price, observedOn);
    _pendingSnapshots.Add(_latestSnapshot);
    return true;
  }
  private void EnsureDoesNotConflictCurrentObservation(
    Money price, ObservationDate observedOn
  ) {
    if (_latestSnapshot.IsConflictingWith(price, observedOn)) {
      throw new ConflictingPriceObservationException(Id, observedOn);
    }
  }

  private void EnsureIsLaterObservation(ObservationDate observedOn) {
    if (_latestSnapshot.IsLaterThan(observedOn)) {
      throw new EarlierPriceObservationException(Id, observedOn);
    }
  }

  internal bool IsIllustratedBy(Uri? imageUrl) => _imageUrl?.Value == imageUrl;

  internal bool HasPriceObservation(decimal price, DateOnly observedOn) =>
    _latestSnapshot.MatchesObservation(price, observedOn);

  internal bool HasPendingPriceObservation(decimal price, DateOnly observedOn) =>
    _pendingSnapshots.Any(snapshot => snapshot.MatchesObservation(price, observedOn));

  internal int CountPendingPriceObservations() => _pendingSnapshots.Count;

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