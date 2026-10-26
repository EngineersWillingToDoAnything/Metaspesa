using Metaspesa.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Metaspesa.Database.Configurations;

internal class SuperMarketConfiguration : IEntityTypeConfiguration<SuperMarketDbEntity> {
  public void Configure(EntityTypeBuilder<SuperMarketDbEntity> builder) {
    builder.ToTable("super_markets", "market", t =>
      t.HasComment("Supermarkets where products are available"));

    builder.HasKey(e => e.Id).HasName("pk_super_market");

    builder.Property(e => e.Id)
      .HasColumnName("id")
      .ValueGeneratedNever();

    builder.Property(e => e.Name)
      .HasColumnName("name")
      .UseCollation("reference_name")
      .IsRequired();

    builder.Property(e => e.LogoUrl)
      .HasColumnName("logo_url")
      .IsRequired(false);

    builder.HasIndex(e => e.Name, "idx_super_market_name").IsUnique();
  }
}