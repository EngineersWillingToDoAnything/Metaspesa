using Metaspesa.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Metaspesa.Database.Configurations;

internal class ProductBrandConfiguration : IEntityTypeConfiguration<ProductBrandDbEntity> {
  public void Configure(EntityTypeBuilder<ProductBrandDbEntity> builder) {
    builder.ToTable("product_brands", "market", t =>
      t.HasComment("Brands of products available in the market"));

    builder.HasKey(e => e.Id).HasName("pk_product_brand");

    builder.Property(e => e.Id)
      .HasColumnName("id")
      .ValueGeneratedNever();

    builder.Property(e => e.Name)
      .HasColumnName("name")
      .UseCollation("reference_name")
      .IsRequired();

    builder.HasIndex(e => e.Name, "idx_product_brand_name").IsUnique();
  }
}