using Metaspesa.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Metaspesa.Database.Configurations;

internal class ProductFormatConfiguration : IEntityTypeConfiguration<ProductFormatDbEntity> {
  public void Configure(EntityTypeBuilder<ProductFormatDbEntity> builder) {
    builder.ToTable("product_formats", "market", t =>
      t.HasComment("""
      Different package formats for the same product. For example, a soda can be sold
      in 330 ml cans, 500 ml bottles, or 1 l bottles.
      """));

    builder.HasKey(e => e.Id).HasName("pk_product_format");

    builder.Property(e => e.Id)
      .HasColumnName("id")
      .ValueGeneratedNever();

    builder.Property(e => e.ProductId)
      .HasColumnName("product_id")
      .IsRequired();

    builder.Property(e => e.Quantity)
      .HasColumnName("quantity")
      .HasPrecision(10, 3)
      .IsRequired();

    builder.Property(e => e.UnitOfMeasure)
      .HasColumnName("unit_of_measure")
      .HasMaxLength(4)
      .IsRequired();

    builder.Property(e => e.ImageUrl)
      .HasColumnName("image_url")
      .IsRequired(true);

    builder.HasIndex(e => e.ProductId, "idx_product_format_product_id");
    builder.HasAlternateKey(e => new { e.Id, e.ProductId })
      .HasName("ak_product_format_id_product_id");
    builder.HasIndex(
        e => new { e.ProductId, e.Quantity, e.UnitOfMeasure },
        "idx_product_format_identity")
      .IsUnique();

    builder.ToTable(t => t.HasCheckConstraint(
      "chk_product_format_positive_quantity", "quantity > 0.000"));
    builder.ToTable(t => t.HasCheckConstraint(
      "chk_product_format_unit_of_measure", "unit_of_measure IN ('g', 'ml', 'unit', 'wash', 'dose')"));

    builder.HasOne(e => e.Product)
      .WithMany(e => e.Formats)
      .HasForeignKey(e => e.ProductId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}