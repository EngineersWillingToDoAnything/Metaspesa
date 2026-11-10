using System.Collections.Immutable;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Markets;

public sealed class Product : AggregateRoot {
  private readonly ProductIdentity _identity;
  private readonly List<ProductFormat> _formats;
  private readonly List<ProductFormat> _pendingFormats = [];

  public ProductId Id { get; }

  private Product(
    ProductId id,
    ProductIdentity identity,
    IEnumerable<ProductFormat> formats
  ) {
    Id = id;
    _identity = identity;
    _formats = [.. formats];
  }

  public static Product Create(ProductObservationParams observation) {
    ArgumentNullException.ThrowIfNull(observation);

    var identity = ProductIdentity.Create(observation);
    var product = new Product(new ProductId(Uid.Create()), identity, []);
    product.RegisterObservation(observation);
    return product;
  }

  public static Product Rehydrate(ProductPrimitives primitives) {
    ArgumentNullException.ThrowIfNull(primitives);

    var id = new ProductId(primitives.Id);
    var identity = new ProductIdentity(
      new MarketId(primitives.MarketId),
      new BrandId(primitives.BrandId),
      new ProductName(primitives.Name));
    IEnumerable<ProductFormat> formats = primitives.Formats
      .Select(ProductFormat.Rehydrate);

    return new Product(id, identity, formats);
  }

  public ProductPrimitives ToPrimitives() {
    ImmutableArray<ProductFormatPrimitives> formats = [..
      _formats.Select(format => format.ToPrimitives())
    ];
    ImmutableArray<Guid> pendingFormatIds = [..
      _pendingFormats.Select(format => format.Id.Value)
    ];

    return new ProductPrimitives(
      Id.Value,
      _identity.Name.Value,
      _identity.BrandId.Value,
      _identity.MarketId.Value,
      formats,
      pendingFormatIds);
  }

  public void RegisterObservation(ProductObservationParams observation) {
    ArgumentNullException.ThrowIfNull(observation);
    EnsureHasSameIdentity(observation);
    EnsureObservationIsNotEmpty(observation);

    var observedOn = ObservationDate.Create(observation.ObservedOn);
    List<PreparedFormatObservation> prepared = PrepareFormats(observation);
    var existing = _formats.ToDictionary(format => format.Quantity);

    foreach (PreparedFormatObservation incoming in prepared) {
      if (existing.TryGetValue(incoming.Quantity, out ProductFormat? format)) {
        format.RecordSnapshot(incoming.Price, observedOn);
      } else {
        var newFormat = ProductFormat.Create(incoming, observedOn);
        _formats.Add(newFormat);
        _pendingFormats.Add(newFormat);
      }
    }
  }

  private static List<PreparedFormatObservation> PrepareFormats(
    ProductObservationParams observation
  ) {
    var identities = new HashSet<Quantity>();
    var prepared = new List<PreparedFormatObservation>();
    foreach (ProductFormatObservationParams format in observation.Formats) {
      PreparedFormatObservation incoming = ProductFormat.PrepareObservation(format);
      if (!identities.Add(incoming.Quantity)) {
        throw new DuplicateObservedProductFormatException(incoming.Quantity);
      }
      prepared.Add(incoming);
    }
    return prepared;
  }

  private void EnsureHasSameIdentity(ProductObservationParams observation) {
    var identity = ProductIdentity.Create(observation);
    if (identity != _identity) {
      throw new ProductObservationIdentityMismatchException(_identity, identity);
    }
  }

  private static void EnsureObservationIsNotEmpty(
    ProductObservationParams observation
  ) {
    if (observation.Formats is null || observation.Formats.Count == 0) {
      throw new EmptyProductObservationException();
    }
  }

  public bool IsIdentifiedBy(Guid marketId, Guid brandId, string name) =>
    _identity.MarketId.Value == marketId &&
    _identity.BrandId.Value == brandId &&
    _identity.Name == new ProductName(name);

  public bool IsNamed(string name) => _identity.Name.Value == name;

  public bool OffersFormat(ProductFormatObservationParams observation) =>
    _formats.Any(format =>
      format.Quantity == Quantity.Create(observation.Quantity, observation.UnitOfMeasure));

  public int CountFormats() => _formats.Count;

  public bool HasPriceObservation(
    ProductFormatObservationParams observation, DateOnly observedOn
  ) {
    return _formats.Any(format =>
      format.Quantity == Quantity.Create(observation.Quantity, observation.UnitOfMeasure) &&
      format.HasPriceObservation(observation.Price, observedOn));
  }

  public bool HasPendingFormat(ProductFormatObservationParams observation) {
    return _pendingFormats.Any(format =>
      format.Quantity == Quantity.Create(observation.Quantity, observation.UnitOfMeasure));
  }

  public int CountPendingFormats() => _pendingFormats.Count;

  public bool HasPendingPriceObservation(
    ProductFormatObservationParams observation, DateOnly observedOn
  ) {
    return _formats.Any(format =>
      format.Quantity == Quantity.Create(observation.Quantity, observation.UnitOfMeasure) &&
      format.HasPendingPriceObservation(observation.Price, observedOn));
  }

  public int CountPendingPriceObservations() {
    return _formats.Sum(format => format.CountPendingPriceObservations());
  }
}

public sealed record ProductPrimitives(
  Guid Id,
  string Name,
  Guid BrandId,
  Guid MarketId,
  ImmutableArray<ProductFormatPrimitives> Formats,
  ImmutableArray<Guid> PendingFormatIds
);