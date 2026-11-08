using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Markets;

public sealed class Market : AggregateRoot {
  private readonly MarketName _name;
  private readonly ImageUrl? _logoUrl;

  public MarketId Id { get; }

  private Market(MarketId id, MarketName name, ImageUrl? logoUrl) {
    Id = id;
    _name = name;
    _logoUrl = logoUrl;
  }

  public static Market Create(
    string rawName, Uri? rawLogoUrl = null
  ) {
    var id = new MarketId(Uid.Create());
    var name = new MarketName(rawName);
    ImageUrl? logoUrl = rawLogoUrl is null ? null : new ImageUrl(rawLogoUrl);

    return new(id, name, logoUrl);
  }

  public static Market Rehydrate(MarketPrimitives primitives) {
    ArgumentNullException.ThrowIfNull(primitives);

    var id = new MarketId(primitives.Id);
    var name = new MarketName(primitives.Name);
    ImageUrl? logoUrl = primitives.LogoUrl is null
      ? null
      : new ImageUrl(new Uri(primitives.LogoUrl));

    return new(id, name, logoUrl);
  }

  public MarketPrimitives ToPrimitives() {
    return new MarketPrimitives(Id.Value, _name.Value, _logoUrl?.Value.ToString());
  }
}

#pragma warning disable CA1054, CA1056 // Persistence snapshots intentionally represent URLs as primitive strings.
public sealed record MarketPrimitives(Guid Id, string Name, string? LogoUrl);
#pragma warning restore CA1054, CA1056