using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Database.Entities;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Metaspesa.Database.Repositories;

internal class PostgreSqlPriceSnapshotRepository(
  MainContext context
) : IPriceSnapshotRepository {
  private const int BatchSize = 1_000;

  public async Task<IReadOnlyCollection<PriceSnapshot>> GetLatestForFormatsAsync(
    IReadOnlyCollection<ProductFormatId> productFormatIds,
    CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync(async () => {
    Guid[] ids = [.. productFormatIds.Select(id => id.Value)];
    List<PriceSnapshotDbEntity> snapshots = await context.PriceSnapshots
      .AsNoTracking()
      .Where(snapshot => ids.Contains(snapshot.ProductFormatId))
      .GroupBy(snapshot => snapshot.ProductFormatId)
      .Select(group => group.OrderByDescending(snapshot => snapshot.ObservedAt)
        .ThenByDescending(snapshot => snapshot.Id)
        .First())
      .ToListAsync(cancellationToken);

    return (IReadOnlyCollection<PriceSnapshot>)[
      .. snapshots.Select(snapshot => snapshot.MapToDomain())
    ];
  }, "Couldn't get latest price snapshots.");

  public async Task AppendAsync(
    IReadOnlyCollection<PriceObservation> observations,
    CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync(async () => {
    IEnumerable<PriceSnapshotDbEntity[]> batches = observations
      .Select(observation => new PriceSnapshotDbEntity {
        Id = Uid.Create(),
        ProductFormatId = observation.ProductFormatId.Value,
        PriceAmount = observation.Price.Amount,
        CurrencyCode = "EUR",
        ObservedAt = observation.ObservedAt.Value,
      })
      .Chunk(BatchSize);

    foreach (PriceSnapshotDbEntity[] batch in batches) {
      await context.PriceSnapshots.AddRangeAsync(batch, cancellationToken);
      await context.SaveChangesAsync(cancellationToken);
      context.ChangeTracker.Clear();
    }
  }, "Couldn't append price snapshots.");

  public async Task DeleteForMarketsAsync(
    IReadOnlyCollection<MarketName> marketNames,
    DateTime observedAt,
    CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync(async () => {
    string[] names = [.. marketNames.Select(name => name.Value)];
    IQueryable<PriceSnapshotDbEntity> snapshots =
      from snapshot in context.PriceSnapshots
      join format in context.ProductFormats
        on snapshot.ProductFormatId equals format.Id
      join product in context.Products
        on format.ProductId equals product.Id
      join market in context.SuperMarkets
        on product.SuperMarketId equals market.Id
      where names.Contains(market.Name) && snapshot.ObservedAt == observedAt
      select snapshot;

    await snapshots.ExecuteDeleteAsync(cancellationToken);
  }, "Couldn't delete price snapshots.");
}