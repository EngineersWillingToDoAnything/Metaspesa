using Metaspesa.Database.Entities;
using Metaspesa.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Metaspesa.Database.Configurations;

internal class UserRoleConfiguration : IEntityTypeConfiguration<UserRoleDbEntity> {
  public void Configure(EntityTypeBuilder<UserRoleDbEntity> builder) {
    builder.ToTable("roles", "identity", t =>
      t.HasComment("User roles for access control"));
    builder.HasKey(e => e.Id).HasName("pk_role");

    builder.Property(e => e.Id)
      .HasColumnName("id")
      .ValueGeneratedNever();

    builder.Property(e => e.Name)
      .HasColumnName("name")
      .HasMaxLength(100)
      .IsRequired();

    builder.HasIndex(e => e.Name, "idx_role_name")
      .IsUnique();

    builder.Property(e => e.Description)
      .HasColumnName("description")
      .HasMaxLength(255)
      .IsRequired();

    builder.HasData(
      new UserRoleDbEntity {
        Id = UserRoleIds.Shopper,
        Name = nameof(Role.Shopper),
        Description = "Regular user who manages shopping lists",
      },
      new UserRoleDbEntity {
        Id = UserRoleIds.ProductManager,
        Name = nameof(Role.ProductManager),
        Description = "User who manages market products",
      },
      new UserRoleDbEntity {
        Id = UserRoleIds.CatalogAdmin,
        Name = nameof(Role.CatalogAdmin),
        Description = "Operator who creates Market and Brand master data",
      }
    );
  }
}