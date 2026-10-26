using System.Data;
using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Domain.Markets;
using Microsoft.EntityFrameworkCore;

namespace Metaspesa.Database.Repositories;

internal class PostgreSqlMarketRepository(
  MainContext context
) : IMarketRepository {
  public async Task<IReadOnlyDictionary<string, Guid>> ResolveMarketsAsync(
    IReadOnlyCollection<string> marketNames, CancellationToken cancellationToken
  ) {
    string[] keys = [.. marketNames
      .Select(name => name.Trim())
      .Distinct(StringComparer.InvariantCultureIgnoreCase)
    ];

    return await context.SuperMarkets.AsNoTracking()
      .Where(entity => keys.Contains(entity.Name))
      .ToDictionaryAsync(
        entity => entity.Name,
        entity => entity.Id,
        StringComparer.InvariantCultureIgnoreCase,
        cancellationToken);
  }

  public async Task<bool> ExistsAsync(
    string marketName, CancellationToken cancellationToken
  ) {
    string normalizedName = marketName.Trim();
    return await context.SuperMarkets.AsNoTracking()
      .AnyAsync(entity => entity.Name == normalizedName, cancellationToken);
  }

  public async Task SaveAsync(Market market, CancellationToken cancellationToken) {
    Guid id = market.Id.Value;
    string name = market.Name.Value;
    string? logo = market.LogoUrl?.Value.ToString();

    await context.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO market.super_markets (id, name, logo_url)
      VALUES ({id}, {name}, {logo})
      ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, logo_url = EXCLUDED.logo_url
      """, cancellationToken);
  }

  public async Task<IReadOnlyCollection<MarketSummary>> GetMarketSummariesAsync(
    CancellationToken cancellationToken
  ) {
    return await context.SuperMarkets
      .OrderBy(market => market.Name)
      .ThenBy(market => market.Id)
      .Select(market => new MarketSummary(
        market.Id,
        market.Name,
        market.LogoUrl == null ? null : new Uri(market.LogoUrl)))
      .ToListAsync(cancellationToken);
  }
}