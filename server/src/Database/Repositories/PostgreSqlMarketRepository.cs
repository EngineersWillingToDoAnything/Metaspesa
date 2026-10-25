using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Database.Entities;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Metaspesa.Database.Repositories;

internal class PostgreSqlMarketRepository(
  MainContext context
) : IMarketRepository {
  public async Task<IReadOnlyCollection<MarketSummary>> GetMarketSummariesAsync(
    CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync(
    async () => await context.SuperMarkets
      .OrderBy(market => market.Name)
      .ThenBy(market => market.Id)
      .Select(market => new MarketSummary(
        market.Id,
        market.Name,
        market.LogoUrl == null ? null : new Uri(market.LogoUrl)))
      .ToListAsync(cancellationToken),
    "Couldn't get market summaries.");

  public async Task<List<Market>> GetMarketsAsync(
    CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync(async () => {
    List<SuperMarketDbEntity> entities =
      await context.SuperMarkets.AsNoTracking().ToListAsync(cancellationToken);

    List<Market> markets = [
      ..entities.Select(entity => new Market(
        new MarketId(entity.Id),
        new MarketName(entity.Name),
        ToImageUrl(entity.LogoUrl)))
    ];
    return markets;
  }, "Couldn't get markets.");

  public async Task AddMarketsAsync(
    IReadOnlyCollection<MarketName> marketNames,
    CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync(async () => {
    context.SuperMarkets.AddRange(
      marketNames.Select(name => new SuperMarketDbEntity {
        Id = Uid.Create(),
        Name = name.Value,
      }));
    await context.SaveChangesAsync(cancellationToken);
  }, "Couldn't add markets.");

  public async Task DeleteMarketsAsync(
    IReadOnlyCollection<MarketName> marketNames,
    CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync(async () => {
    string[] names = [.. marketNames.Select(name => name.Value)];
    List<Guid> productIds = await context.Products
      .Where(product => names.Contains(product.SuperMarket.Name))
      .Select(product => product.Id)
      .ToListAsync(cancellationToken);

    await context.PriceSnapshots
      .Where(snapshot =>
        productIds.Contains(snapshot.ProductFormat.ProductId))
      .ExecuteDeleteAsync(cancellationToken);
    await context.ProductFormats
      .Where(format => productIds.Contains(format.ProductId))
      .ExecuteDeleteAsync(cancellationToken);
    await context.Products
      .Where(product => productIds.Contains(product.Id))
      .ExecuteDeleteAsync(cancellationToken);
    await context.SuperMarkets
      .Where(market => names.Contains(market.Name))
      .ExecuteDeleteAsync(cancellationToken);
  }, "Couldn't delete markets.");

  private static ImageUrl? ToImageUrl(string? value) =>
    string.IsNullOrWhiteSpace(value)
      ? null
      : new ImageUrl(new Uri(value, UriKind.Absolute));
}