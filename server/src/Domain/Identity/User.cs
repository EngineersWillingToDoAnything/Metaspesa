using Metaspesa.Domain.Identity.Errors;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Identity;

public class User : AggregateRoot {
  public UserId Id { get; }
  public Username Username { get; }
  public PasswordHash PasswordHash { get; private set; }
  public Role Role { get; private set; }

  private User(
    UserId id, Username username, PasswordHash passwordHash, Role role
  ) {
    EnsureValidRole(role);

    Id = id;
    Username = username;
    PasswordHash = passwordHash;
    Role = role;
  }

  public static User Create(
    string primitiveUsername, string primitivePasswordHash
  ) {
    var id = new UserId(Uid.Create());
    var username = new Username(primitiveUsername);
    var passwordHash = new PasswordHash(primitivePasswordHash);

    return new User(id, username, passwordHash, Role.Shopper);
  }

  public static User Rehydrate(
    UserId id,
    Username username,
    PasswordHash passwordHash,
    Role role
  ) => new(id, username, passwordHash, role);

  private static void EnsureValidRole(Role role) {
    if (role is Role.None || !Enum.IsDefined(role)) {
      throw new InvalidRoleException(role);
    }
  }
}