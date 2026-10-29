using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Markets;

public sealed class ProductFormat {
  public ProductFormatId Id { get; }
  public Quantity Quantity { get; }
  public ImageUrl? ImageUrl { get; }
  public PriceSnapshot LatestSnapshot { get; private set; }

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
    PreparedFormatObservation observation, ObservationTime observedAt
  ) {
    var id = new ProductFormatId(Uid.Create());
    return new ProductFormat(
      id,
      observation.Quantity,
      observation.ImageUrl,
      PriceSnapshot.Create(id, observation.Price, observedAt));
  }

  public static ProductFormat Rehydrate(
    ProductFormatId id,
    Quantity quantity,
    ImageUrl? imageUrl,
    PriceSnapshot latestSnapshot
  ) {
    return new ProductFormat(id, quantity, imageUrl, latestSnapshot);
  }

  internal bool RecordSnapshot(Money price, ObservationTime observedAt) {
    EnsureIsLaterObservation(observedAt);
    EnsureDoesNotConflictCurrentObservation(price, observedAt);

    if (LatestSnapshot.HasSamePrice(price)) {
      return false;
    }

    LatestSnapshot = PriceSnapshot.Create(Id, price, observedAt);
    return true;
  }

  private void EnsureDoesNotConflictCurrentObservation(
    Money price, ObservationTime observedAt
  ) {
    if (LatestSnapshot.IsConflictingWith(price, observedAt)) {
      throw new ConflictingPriceObservationException(Id, observedAt);
    }
  }

  private void EnsureIsLaterObservation(ObservationTime observedAt) {
    if (LatestSnapshot.IsLaterThan(observedAt)) {
      throw new EarlierPriceObservationException(
        Id, observedAt, LatestSnapshot.ObservedAt);
    }
  }
}

internal sealed record PreparedFormatObservation(
  Quantity Quantity, Money Price, ImageUrl ImageUrl);