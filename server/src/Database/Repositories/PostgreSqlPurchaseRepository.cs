using Metaspesa.Application.Abstractions.Purchasing;
using Metaspesa.Database.Entities;
using Metaspesa.Domain.Shopping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Metaspesa.Database.Repositories;

internal class PostgreSqlPurchaseRepository(
  MainContext context
) : IPurchaseRepository {
  public async Task<Purchase?> GetAsync(
    Guid purchaseId, CancellationToken cancellationToken
  ) {
    PurchaseDbEntity? entity = await context.Purchases
      .AsNoTracking()
      .Include(purchase => purchase.Items)
      .Where(purchase => purchase.Id == purchaseId)
      .FirstOrDefaultAsync(cancellationToken);

    return entity is null ? null : ToDomain(entity);
  }

  public async Task SaveAsync(
    Purchase purchase, CancellationToken cancellationToken
  ) {
    ArgumentNullException.ThrowIfNull(purchase);
    Guid purchaseId = purchase.Id.Value;
    Guid? buyerId = purchase.BuyerId?.Value;
    Guid? shoppingListId = purchase.ShoppingListId?.Value;
    PurchaseItem[] items = [.. purchase.Items];

    var itemIds = new Guid[items.Length];
    var snapshotIds = new Guid[items.Length];
    int[] amounts = new int[items.Length];
    for (int i = 0; i < items.Length; i++) {
      PurchaseItem item = items[i];
      itemIds[i] = item.Id.Value;
      snapshotIds[i] = item.PriceSnapshotId.Value;
      amounts[i] = item.Amount.Value;
    }

    await using IDbContextTransaction transaction = await context.Database
      .BeginTransactionAsync(cancellationToken);

    await context.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO shopping.purchases (id, user_uid, shopping_list_id, purchased_at)
      VALUES ({purchaseId}, {buyerId}, {shoppingListId}, {purchase.PurchasedAt})
      ON CONFLICT (id) DO UPDATE SET
        user_uid = EXCLUDED.user_uid,
        shopping_list_id = EXCLUDED.shopping_list_id,
        purchased_at = EXCLUDED.purchased_at;

      DELETE FROM shopping.purchase_items
      WHERE purchase_id = {purchaseId};

      INSERT INTO shopping.purchase_items (
        id, purchase_id, price_snapshot_id, amount
      )
      SELECT item_id, {purchaseId}, price_snapshot_id, amount
      FROM unnest({itemIds}, {snapshotIds}, {amounts})
        AS items(item_id, price_snapshot_id, amount);
      """, cancellationToken);

    await transaction.CommitAsync(cancellationToken);
  }

  private static Purchase ToDomain(PurchaseDbEntity entity) {
    return Purchase.Rehydrate(
      entity.Id,
      entity.UserUid,
      entity.ShoppingListId,
      entity.PurchasedAt,
      [.. entity.Items.Select(item =>
        PurchaseItem.Rehydrate(item.Id, item.PriceSnapshotId, item.Amount))
      ]);
  }
}