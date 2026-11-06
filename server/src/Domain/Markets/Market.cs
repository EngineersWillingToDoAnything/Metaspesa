using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Markets;

public sealed class Market : AggregateRoot {
  public MarketId Id { get; }
  public MarketName Name { get; }
  public ImageUrl? LogoUrl { get; }

  private Market(MarketId id, MarketName name, ImageUrl? logoUrl) {
    Id = id;
    Name = name;
    LogoUrl = logoUrl;
  }

  public static Market Create(
    string rawName, Uri? rawLogoUrl = null
  ) {
    var id = new MarketId(Uid.Create());
    var name = new MarketName(rawName);
    ImageUrl? logoUrl = rawLogoUrl is null ? null : new ImageUrl(rawLogoUrl);

    return new(id, name, logoUrl);
  }

  public static Market Rehydrate(
    MarketId id, MarketName name, ImageUrl? logoUrl
  ) {
    return new(id, name, logoUrl);
  }

  public MarketPrimitives ToPrimitives() {
    return new MarketPrimitives(Id.Value, Name.Value, LogoUrl?.Value.ToString());
  }
}

#pragma warning disable CA1054, CA1056 // Persistence snapshots intentionally represent URLs as primitive strings.
public sealed record MarketPrimitives(Guid Id, string Name, string? LogoUrl);
#pragma warning restore CA1054, CA1056