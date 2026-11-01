using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Markets;

public sealed class Market {
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

  public static Market Hydrate(
    MarketId id, MarketName name, ImageUrl? logoUrl
  ) {
    return new(id, name, logoUrl);
  }
}