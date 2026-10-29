using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Markets;

public sealed class Product {
  private readonly List<ProductFormat> _formats;
  private readonly List<ProductFormat> _pendingFormats = [];
  private readonly List<PriceSnapshot> _pendingSnapshots = [];
  private readonly ProductIdentity _identity;

  public ProductId Id { get; }
  public ProductName Name => _identity.Name;
  public BrandId BrandId => _identity.BrandId;
  public MarketId MarketId => _identity.MarketId;
  public IReadOnlyCollection<ProductFormat> Formats => _formats.AsReadOnly();
  public IReadOnlyCollection<ProductFormat> PendingFormats => _pendingFormats.AsReadOnly();
  public IReadOnlyCollection<PriceSnapshot> PendingSnapshots => _pendingSnapshots.AsReadOnly();

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

  public static Product Rehydrate(
    ProductId id,
    ProductName name,
    BrandId brandId,
    MarketId marketId,
    IEnumerable<ProductFormat> formats
  ) {
    return new Product(id, new ProductIdentity(marketId, brandId, name), formats);
  }

  public void RegisterObservation(ProductObservationParams observation) {
    ArgumentNullException.ThrowIfNull(observation);
    EnsureHasSameIdentity(observation);

    var observedAt = ObservationTime.Create(observation.ObservedAt);
    List<PreparedFormatObservation> prepared = PrepareFormats(observation);
    var existing = _formats.ToDictionary(format => format.Quantity);
    var additions = new List<ProductFormat>();
    var updates = new List<(ProductFormat Format, Money Price)>();

    foreach (PreparedFormatObservation incoming in prepared) {
      if (existing.TryGetValue(incoming.Quantity, out ProductFormat? format)) {
        updates.Add((format, incoming.Price));
      } else {
        additions.Add(ProductFormat.Create(incoming, observedAt));
      }
    }

    _formats.AddRange(additions);
    _pendingFormats.AddRange(additions);
    _pendingSnapshots.AddRange(additions.Select(format => format.LatestSnapshot));
    foreach ((ProductFormat format, Money price) in updates) {
      if (format.RecordSnapshot(price, observedAt)) {
        _pendingSnapshots.Add(format.LatestSnapshot);
      }
    }
  }

  private static List<PreparedFormatObservation> PrepareFormats(
    ProductObservationParams observation
  ) {
    EnsureObservationIsNotEmpty(observation);

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
}