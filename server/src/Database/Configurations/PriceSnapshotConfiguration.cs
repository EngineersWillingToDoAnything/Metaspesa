using Metaspesa.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Metaspesa.Database.Configurations;

internal class PriceSnapshotConfiguration : IEntityTypeConfiguration<PriceSnapshotDbEntity> {
  public void Configure(EntityTypeBuilder<PriceSnapshotDbEntity> builder) {
    builder.ToTable("price_snapshots", "market", t =>
      t.HasComment("""
      Historical price observations for product formats.
      """));

    builder.HasKey(e => e.Id).HasName("pk_price_snapshot");

    builder.Property(e => e.Id)
      .HasColumnName("id")
      .ValueGeneratedNever();

    builder.Property(e => e.ProductFormatId)
      .HasColumnName("product_format_id")
      .IsRequired();

    builder.Property(e => e.PriceAmount)
      .HasColumnName("price_amount")
      .HasPrecision(10, 2)
      .IsRequired();

    builder.Property(e => e.CurrencyCode)
      .HasColumnName("currency_code")
      .HasColumnType("char(3)")
      .HasDefaultValue("EUR")
      .IsRequired();

    builder.Property(e => e.ObservedOn)
      .HasColumnName("observed_on")
      .HasColumnType("date")
      .HasDefaultValueSql("(now() AT TIME ZONE 'UTC')::date")
      .IsRequired();

    builder.ToTable(t => t.HasCheckConstraint(
      "chk_price_snapshot_positive_price", "price_amount > 0.00"));
    builder.ToTable(t => t.HasCheckConstraint(
      "chk_price_snapshot_currency_code", "currency_code ~ '^[A-Z]{3}$'"));

    builder.HasIndex(e => e.ProductFormatId, "idx_price_snapshot_product_format_id");
    builder.HasIndex(e => e.ObservedOn, "idx_price_snapshot_observed_on");
    builder.HasIndex(
      e => new { e.ProductFormatId, e.ObservedOn },
      "idx_price_snapshot_format_observed_on");

    builder.HasOne(e => e.ProductFormat)
      .WithMany(e => e.PriceSnapshots)
      .HasForeignKey(e => e.ProductFormatId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}