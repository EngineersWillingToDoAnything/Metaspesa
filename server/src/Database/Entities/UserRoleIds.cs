using Metaspesa.Domain.Identity;

namespace Metaspesa.Database.Entities;

internal static class UserRoleIds {
  public static readonly Guid Shopper =
    Guid.Parse("00000000-0000-7000-8000-000000000001");
  public static readonly Guid ProductManager =
    Guid.Parse("00000000-0000-7000-8000-000000000002");
  public static readonly Guid CatalogAdmin =
    Guid.Parse("00000000-0000-7000-8000-000000000003");

  public static Guid FromRole(Role role) => role switch {
    Role.Shopper => Shopper,
    Role.ProductManager => ProductManager,
    Role.CatalogAdmin => CatalogAdmin,
    _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported role."),
  };
}