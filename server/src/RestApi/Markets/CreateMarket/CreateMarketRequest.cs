using System.Text.Json.Serialization;

namespace Metaspesa.RestApi.Markets.CreateMarket;

/// <summary>Display information for a new Market. The server assigns its ID.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record CreateMarketRequest(string Name, Uri? LogoUrl = null);