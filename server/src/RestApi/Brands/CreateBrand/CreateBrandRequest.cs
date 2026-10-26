using System.Text.Json.Serialization;

namespace Metaspesa.RestApi.Brands.CreateBrand;

/// <summary>Display name for a new Brand. The server assigns its ID.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record CreateBrandRequest(string Name);