using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Metaspesa.RestApi.UnitTests.Shopping;

internal static class ShoppingEndpointTestData {
  internal static readonly Guid OwnerUid =
    Guid.Parse("e5740719-bf47-4f79-9005-0dd70c522f14");
  internal static readonly Guid ListId = Guid.CreateVersion7();
  internal static readonly Guid FormatId = Guid.CreateVersion7();
  internal static readonly Guid MarketUid = Guid.CreateVersion7();

  internal static DefaultHttpContext ShopperContext() => new() {
    User = new ClaimsPrincipal(new ClaimsIdentity([
      new Claim(JwtRegisteredClaimNames.Sub, OwnerUid.ToString()),
    ])),
  };
}