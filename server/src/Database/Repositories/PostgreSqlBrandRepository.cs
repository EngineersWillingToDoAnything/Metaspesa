using System.Data;
using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Domain.Markets;
using Microsoft.EntityFrameworkCore;

namespace Metaspesa.Database.Repositories;

internal sealed class PostgreSqlBrandRepository(
  MainContext context
) : IBrandRepository {
  public async Task<IReadOnlyDictionary<string, Guid>> ResolveBrandsAsync(
    IReadOnlyCollection<string> brandNames, CancellationToken cancellationToken
  ) {
    string[] keys = [.. brandNames
      .Select(name => name.Trim())
      .Distinct(StringComparer.InvariantCultureIgnoreCase)
    ];

    return await context.ProductBrands.AsNoTracking()
      .Where(entity => keys.Contains(entity.Name))
      .ToDictionaryAsync(
        entity => entity.Name,
        entity => entity.Id,
        StringComparer.InvariantCultureIgnoreCase,
        cancellationToken);
  }

  public async Task<bool> ExistsAsync(
    string brandName, CancellationToken cancellationToken
  ) {
    string normalizedName = brandName.Trim();

    return await context.ProductBrands.AsNoTracking()
      .AnyAsync(entity => entity.Name == normalizedName, cancellationToken);
  }

  public async Task SaveAsync(Brand brand, CancellationToken cancellationToken) {
    Guid id = brand.Id.Value;
    string name = brand.Name.Value;

    await context.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO market.product_brands (id, name) VALUES ({id}, {name})
      ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name
      """, cancellationToken);
  }
}