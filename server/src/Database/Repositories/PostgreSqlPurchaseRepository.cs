using Metaspesa.Application.Abstractions.Shopping;
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
    PurchasePrimitives state = purchase.ToPrimitives();
    PurchaseItemPrimitives[] items = [.. state.Items];

    var itemIds = new Guid[items.Length];
    var snapshotIds = new Guid[items.Length];
    int[] amounts = new int[items.Length];
    for (int i = 0; i < items.Length; i++) {
      PurchaseItemPrimitives item = items[i];
      itemIds[i] = item.Id;
      snapshotIds[i] = item.PriceSnapshotId;
      amounts[i] = item.Amount;
    }

    await using IDbContextTransaction transaction = await context.Database
      .BeginTransactionAsync(cancellationToken);

    await context.Database.ExecuteSqlInterpolatedAsync($"""
      WITH inserted_purchase AS (
        INSERT INTO shopping.purchases (id, user_uid, shopping_list_id, purchased_at)
        VALUES ({state.Id}, {state.BuyerId}, {state.ShoppingListId}, {state.PurchasedAt})
        ON CONFLICT (id) DO NOTHING
        RETURNING id
      )
      INSERT INTO shopping.purchase_items (
        id, purchase_id, price_snapshot_id, amount
      )
      SELECT item_id, inserted_purchase.id, price_snapshot_id, amount
      FROM inserted_purchase
      CROSS JOIN unnest({itemIds}, {snapshotIds}, {amounts})
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