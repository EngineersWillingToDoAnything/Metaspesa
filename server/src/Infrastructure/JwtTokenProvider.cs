using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Metaspesa.Application.Abstractions.Users;
using Metaspesa.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Metaspesa.Infrastructure;

internal class JwtTokenProvider(
  IOptions<JwtOptions> options, TimeProvider timeProvider
) : ITokenProvider {
  public Token GenerateToken(User user) {
    Debug.Assert(user is not null);
    Debug.Assert(options.Value.Key is not null);

    JwtOptions jwtOptions = options.Value;
    SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(jwtOptions.Key));
    SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);
    DateTime expiresAt = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(jwtOptions.ExpirationMinutes);

    Claim[] claims = [
      new(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()),
      new(JwtRegisteredClaimNames.Name, user.Username.Value),
      new(ClaimTypes.Role, user.Role.ToString()),
    ];

    JwtSecurityToken token = new(
      issuer: jwtOptions.Issuer,
      audience: jwtOptions.Audience,
      claims: claims,
      expires: expiresAt,
      signingCredentials: credentials
    );

    return new Token(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
  }
}