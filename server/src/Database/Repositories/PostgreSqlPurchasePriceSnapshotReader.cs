using Metaspesa.Application.Abstractions.Shopping;
using Microsoft.EntityFrameworkCore;

namespace Metaspesa.Database.Repositories;

internal class PostgreSqlPurchasePriceSnapshotReader(
  MainContext context
) : IPurchasePriceSnapshotReader {
  public async Task<IReadOnlyDictionary<Guid, Guid>> GetSnapshotsIdAsync(
    IReadOnlyCollection<Guid> productFormatIds,
    DateTime purchasedAt,
    CancellationToken cancellationToken
  ) {
    if (productFormatIds.Count == 0) {
      return new Dictionary<Guid, Guid>();
    }

    Guid[] formatIds = [.. productFormatIds.Distinct()];
    Dictionary<Guid, Guid> snapshots = await context.PriceSnapshots
      .AsNoTracking()
      .Where(snapshot => formatIds.Contains(snapshot.ProductFormatId) &&
        snapshot.ObservedAt <= purchasedAt)
      .GroupBy(snapshot => snapshot.ProductFormatId)
      .Select(group => group
        .OrderByDescending(snapshot => snapshot.ObservedAt)
        .ThenByDescending(snapshot => snapshot.Id)
        .Select(snapshot => new {
          snapshot.ProductFormatId,
          SnapshotId = snapshot.Id,
        })
        .First())
      .ToDictionaryAsync(
        snapshot => snapshot.ProductFormatId,
        snapshot => snapshot.SnapshotId,
        cancellationToken);

    return snapshots;
  }
}