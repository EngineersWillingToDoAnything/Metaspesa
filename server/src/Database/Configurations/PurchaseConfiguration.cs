using Metaspesa.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Metaspesa.Database.Configurations;

internal class PurchaseConfiguration : IEntityTypeConfiguration<PurchaseDbEntity> {
  public void Configure(EntityTypeBuilder<PurchaseDbEntity> builder) {
    builder.ToTable("purchases", "shopping", t =>
      t.HasComment("""
      Purchase receipt header. It records who bought, when, and optionally which
      shopping list was checked out. Product lines live in purchase_items.
      """));

    builder.HasKey(e => e.Id).HasName("pk_purchase");

    builder.Property(e => e.Id)
      .HasColumnName("id")
      .ValueGeneratedNever();

    builder.Property(e => e.UserUid)
      .HasColumnName("user_uid")
      .IsRequired(false);

    builder.Property(e => e.ShoppingListId)
      .HasColumnName("shopping_list_id")
      .IsRequired(false);

    builder.Property(e => e.PurchasedAt)
      .HasColumnName("purchased_at")
      .HasDefaultValueSql("now()")
      .IsRequired();

    builder.HasIndex(e => e.UserUid, "idx_purchases_user_uid");
    builder.HasIndex(e => e.ShoppingListId, "idx_purchases_shopping_list_id");
    builder.HasIndex(e => e.PurchasedAt, "idx_purchases_purchased_at");

    builder.HasOne(e => e.User)
      .WithMany(e => e.Purchases)
      .HasForeignKey(e => e.UserUid)
      .OnDelete(DeleteBehavior.SetNull);

    builder.HasOne(e => e.ShoppingList)
      .WithMany(e => e.Purchases)
      .HasForeignKey(e => e.ShoppingListId)
      .OnDelete(DeleteBehavior.SetNull);
  }
}