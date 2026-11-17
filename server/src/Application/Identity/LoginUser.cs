using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Users;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Identity.Errors;

namespace Metaspesa.Application.Identity;

public static class LoginUser {
  public record Query(string Username, string Password);

  public class Handler(
    IUserRepository userRepository,
    IHasher hasher,
    ITokenProvider tokenProvider
  ) : IQueryHandler<Query, Token> {
    public async Task<Token> Handle(
      Query query, CancellationToken cancellationToken = default
    ) {
      ArgumentNullException.ThrowIfNull(query);

      User? user = await userRepository.GetUserByUsernameAsync(
        new Username(query.Username),
        cancellationToken);

      if (user is null || !hasher.VerifyHash(query.Password, user.PasswordHash.Value)) {
        throw new InvalidCredentialsException();
      }

      return tokenProvider.GenerateToken(user);
    }
  }
}