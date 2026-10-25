using Metaspesa.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Metaspesa.Database;

internal class MainContext(
  DbContextOptions<MainContext> options
) : DbContext(options) {
  public DbSet<UserDbEntity> Users { get; set; } = null!;
  public DbSet<UserRoleDbEntity> UserRoles { get; set; } = null!;

  public DbSet<ShoppingListDbEntity> ShoppingLists { get; set; } = null!;
  public DbSet<ShoppingItemDbEntity> ShoppingItems { get; set; } = null!;
  public DbSet<ShoppingListOwnershipDbEntity> ShoppingListOwnerships { get; set; } = null!;
  public DbSet<PurchaseDbEntity> Purchases { get; set; } = null!;
  public DbSet<PurchaseItemDbEntity> PurchaseItems { get; set; } = null!;

  public DbSet<SuperMarketDbEntity> SuperMarkets { get; set; } = null!;
  public DbSet<ProductBrandDbEntity> ProductBrands { get; set; } = null!;
  public DbSet<ProductDbEntity> Products { get; set; } = null!;
  public DbSet<ProductFormatDbEntity> ProductFormats { get; set; } = null!;
  public DbSet<PriceSnapshotDbEntity> PriceSnapshots { get; set; } = null!;

  protected override void OnModelCreating(ModelBuilder modelBuilder) {
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(MainContext).Assembly);
  }
}