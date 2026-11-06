using Metaspesa.Application.Abstractions.Users;
using Metaspesa.Database.Entities;
using Metaspesa.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Metaspesa.Database.Repositories;

internal partial class PostgreSqlUserRepository(
  MainContext context
  ) : IUserRepository {
  public async Task<bool> CheckUsernameExistsAsync(
    string username, CancellationToken cancellationToken = default
  ) => await PostgreSqlExceptionMapper.MapAsync(
    async () => await context.Users.AnyAsync(
      u => EF.Functions.ILike(u.Username, username.Trim()), cancellationToken),
    "Couldn't check if username exists.");

  public async Task SaveAsync(
    User user, CancellationToken cancellationToken = default
  ) {
    UserPrimitives state = user.ToPrimitives();
    context.Users.Add(new UserDbEntity {
      Uid = state.Id,
      Username = state.Username,
      EncryptedPassword = state.PasswordHash,
      RoleId = UserRoleIds.FromRole((Role)state.Role),
    });
    await context.SaveChangesAsync(cancellationToken);
  }

  public async Task<User?> GetUserByUsernameAsync(
    Username username, CancellationToken cancellationToken = default
  ) => await PostgreSqlExceptionMapper.MapAsync(async () => {
    UserDbEntity? entity = await context.Users
      .Include(u => u.Role)
      .FirstOrDefaultAsync(
        u => EF.Functions.ILike(u.Username, username.Value), cancellationToken);

    if (entity is null) {
      return null;
    }

    return Enum.TryParse(entity.Role.Name, out Role role)
      ? User.Rehydrate(
        new UserId(entity.Uid),
        new Username(entity.Username),
        new PasswordHash(entity.EncryptedPassword),
        role)
      : null;
  }, "Couldn't get user by username.");
}