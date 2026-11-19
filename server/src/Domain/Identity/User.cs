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
    Id = id;
    Username = username;
    PasswordHash = passwordHash;
    Role = role;
  }

  public static User Create(
    string primitiveUsername, string primitivePasswordHash
  ) {
    var id = UserId.Create(Uid.Create());
    var username = Username.Create(primitiveUsername);
    var passwordHash = PasswordHash.Create(primitivePasswordHash);

    return new User(id, username, passwordHash, Role.Shopper);
  }

  public static User Rehydrate(UserPrimitives primitives) {
    ArgumentNullException.ThrowIfNull(primitives);

    return new User(
      new UserId(primitives.Id),
      new Username(primitives.Username),
      new PasswordHash(primitives.PasswordHash),
      (Role)primitives.Role);
  }

  public UserPrimitives ToPrimitives() {
    return new UserPrimitives(
      Id.Value, Username.Value, PasswordHash.Value, (int)Role);
  }

}

public sealed record UserPrimitives(Guid Id, string Username, string PasswordHash, int Role);