using Metaspesa.Application.Abstractions.Purchasing;
using Metaspesa.Database.Entities;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.Database.Repositories;

internal class PostgreSqlPurchaseRepository(
  MainContext context
) : IPurchaseRepository {
  public async Task<Guid> AddAsync(
    Purchase purchase, CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync(async () => {
    ArgumentNullException.ThrowIfNull(purchase);
    var entity = new PurchaseDbEntity {
      Id = purchase.Id.Value,
      UserUid = purchase.BuyerId?.Value,
      ShoppingListId = purchase.ShoppingListId?.Value,
      PurchasedAt = purchase.PurchasedAt,
      Items = [.. purchase.Items.Select(item => new PurchaseItemDbEntity {
        Id = Uid.Create(),
        PriceSnapshotId = item.PriceSnapshotId.Value,
        Amount = item.Amount.Value,
      })],
    };
    context.Purchases.Add(entity);
    await context.SaveChangesAsync(cancellationToken);
    return entity.Id;
  }, "Couldn't create purchase.");
}