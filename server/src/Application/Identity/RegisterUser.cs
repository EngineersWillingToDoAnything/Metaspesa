using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Users;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Identity.Errors;

namespace Metaspesa.Application.Identity;

public static class RegisterUser {
  public record Command(string Username, string Password);

  public class Handler(
    IHasher hasher,
    IUserRepository userRepository
  ) : ICommandHandler<Command> {
    public async Task Handle(
      Command command, CancellationToken cancellationToken = default
    ) {
      ArgumentNullException.ThrowIfNull(command);

      PasswordPolicy.EnsureIsValid(command.Password);

      if (await userRepository.CheckUsernameExistsAsync(command.Username, cancellationToken)) {
        throw new UsernameAlreadyExistsException(command.Username);
      }

      string hashedPassword = hasher.Hash(command.Password);
      var user = User.Create(command.Username, hashedPassword);

      await userRepository.SaveAsync(user, cancellationToken);
    }
  }
}