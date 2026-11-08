using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Database.Entities;
using Metaspesa.Domain.Markets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Metaspesa.Database.Repositories;

internal class PostgreSqlProductRepository(
  MainContext context
) : IProductRepository {
  public async Task<Product?> GetByIdAsync(
    ProductId productId, CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync(async () => {
    ProductDbEntity? entity = await context.Products
      .AsNoTracking()
      .Include(product => product.Formats)
      .ThenInclude(format => format.PriceSnapshots
        .OrderByDescending(snapshot => snapshot.ObservedOn)
        .ThenByDescending(snapshot => snapshot.Id)
        .Take(1))
      .SingleOrDefaultAsync(
        product => product.Id == productId.Value,
        cancellationToken);

    return entity?.ToAggregate();
  }, "Couldn't get product.");

  public async Task<PagedResult<CatalogProduct>> GetProductsAsync(
    GetMarketProductsFilter filter, CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync(async () => {
    IQueryable<ProductDbEntity> query = context.Products
      .AsNoTracking()
      .Where(product => product.Formats.Any(format => format.PriceSnapshots.Count > 0));

    if (!string.IsNullOrWhiteSpace(filter.NameSegment)) {
      query = query.Where(product => EF.Functions.ILike(
        EF.Functions.Collate(product.Name, "default"), $"%{EscapeLike(filter.NameSegment)}%", "\\"));
    }
    if (filter.MarketIds.Count > 0) {
      Guid[] marketIds = [.. filter.MarketIds.Select(id => id.Value)];
      query = query.Where(product => marketIds.Contains(product.SuperMarketId));
    }
    if (!string.IsNullOrWhiteSpace(filter.BrandNameSegment)) {
      query = query.Where(product => EF.Functions.ILike(
        EF.Functions.Collate(product.Brand.Name, "default"), $"%{EscapeLike(filter.BrandNameSegment)}%", "\\"));
    }

    int totalCount = await query.CountAsync(cancellationToken);
    IOrderedQueryable<ProductDbEntity> ordered = filter.Sort switch {
      CatalogSort.PriceAsc => query.OrderBy(product => product.Formats
        .Where(format => format.PriceSnapshots.Count > 0)
        .Min(format => format.PriceSnapshots
          .OrderByDescending(snapshot => snapshot.ObservedOn)
          .ThenByDescending(snapshot => snapshot.Id)
          .Select(snapshot => snapshot.PriceAmount).First())),
      CatalogSort.PriceDesc => query.OrderByDescending(product => product.Formats
        .Where(format => format.PriceSnapshots.Count > 0)
        .Min(format => format.PriceSnapshots
          .OrderByDescending(snapshot => snapshot.ObservedOn)
          .ThenByDescending(snapshot => snapshot.Id)
          .Select(snapshot => snapshot.PriceAmount).First())),
      _ => query.OrderBy(product => product.Name),
    };

    List<ProductDbEntity> products = await ordered
      .ThenBy(product => product.Name)
      .ThenBy(product => product.Id)
      .Skip(filter.Pagination.Skip)
      .Take(filter.Pagination.Size)
      .Include(product => product.Brand)
      .Include(product => product.SuperMarket)
      .Include(product => product.Formats)
      .Include(product => product.Formats)
      .ThenInclude(format => format.PriceSnapshots)
      .AsSplitQuery()
      .ToListAsync(cancellationToken);

    return new PagedResult<CatalogProduct>([
      .. products.Select(product => new CatalogProduct(
        product.Id,
        product.Name,
        product.Brand.Name,
        new MarketSummary(product.SuperMarketId, product.SuperMarket.Name,
          ToUri(product.SuperMarket.LogoUrl)),
        [.. product.Formats
          .Where(format => format.PriceSnapshots.Count > 0)
          .OrderBy(format => format.Id)
          .Select(ToCatalogFormat)]))
    ], totalCount);
  }, "Couldn't get market products.");

  private static CatalogFormat ToCatalogFormat(ProductFormatDbEntity format) {
    PriceSnapshotDbEntity snapshot = format.PriceSnapshots
      .OrderByDescending(value => value.ObservedOn)
      .ThenByDescending(value => value.Id)
      .First();
    return new CatalogFormat(
      format.Id,
      format.Quantity,
      format.UnitOfMeasure,
      snapshot.PriceAmount,
      snapshot.CurrencyCode,
      ToUri(format.ImageUrl),
      snapshot.ObservedOn);
  }

  private static Uri? ToUri(string? value) =>
    string.IsNullOrWhiteSpace(value)
      ? null
      : new Uri(value, UriKind.Absolute);

  private static string EscapeLike(string value) => value
    .Replace("\\", "\\\\", StringComparison.Ordinal)
    .Replace("%", "\\%", StringComparison.Ordinal)
    .Replace("_", "\\_", StringComparison.Ordinal);

  public async Task<bool> CheckFormatsExistAsync(
    IEnumerable<Guid> formatIds, CancellationToken cancellationToken
  ) {
    Guid[] ids = [.. formatIds.Distinct()];
    if (ids.Length == 0) { return true; }

    int formatCount = await context.ProductFormats
      .AsNoTracking()
      .Where(format => ids.Contains(format.Id))
      .CountAsync(cancellationToken);

    return formatCount == ids.Length;
  }

  public async Task<IReadOnlyDictionary<ProductIdentity, Product>> GetBatchAsync(
    IReadOnlyCollection<ProductIdentity> identities,
    CancellationToken cancellationToken
  ) {
    ArgumentNullException.ThrowIfNull(identities);

    if (identities.Count == 0) {
      return new Dictionary<ProductIdentity, Product>();
    }

    ProductIdentity[] keys = [.. identities.Distinct()];
    Guid[] marketIds = [.. keys.Select(identity => identity.MarketId.Value)];
    Guid[] brandIds = [.. keys.Select(identity => identity.BrandId.Value)];
    string[] names = [.. keys.Select(identity => identity.Name.Value)];

    List<ProductDbEntity> entities = await context.Products.FromSqlInterpolated($"""
        SELECT product.* FROM market.products AS product
        JOIN unnest({marketIds}, {brandIds}, {names}) AS requested(market_id, brand_id, name)
          ON product.super_market_id = requested.market_id
          AND product.brand_id = requested.brand_id
          AND product.name = requested.name
        """)
      .AsNoTracking()
      .Include(product => product.Formats)
      .ThenInclude(format => format.PriceSnapshots
        .OrderByDescending(snapshot => snapshot.ObservedOn)
        .ThenByDescending(snapshot => snapshot.Id)
        .Take(1))
      .AsSplitQuery()
      .ToListAsync(cancellationToken);

    return entities.ToDictionary(
      entity => entity.ToAggregateIdentity(),
      entity => entity.ToAggregate());
  }

  public async Task SaveBatchAsync(
    IReadOnlyCollection<Product> products,
    CancellationToken cancellationToken
  ) {
    ArgumentNullException.ThrowIfNull(products);

    ProductPrimitives[] states = [.. products.Select(product => product.ToPrimitives())];
    if (states.DistinctBy(product => product.Id).Count() != states.Length) {
      throw new ArgumentException(
        "A batch cannot contain a Product more than once.", nameof(products));
    }

    ProductPrimitives[] changed = [.. states.Where(product =>
      !product.PendingFormatIds.IsEmpty || product.Formats.Any(format => !format.PendingSnapshots.IsEmpty))];
    if (changed.Length == 0) {
      return;
    }

    await using IDbContextTransaction transaction = await context.Database
      .BeginTransactionAsync(cancellationToken);
    Guid[] ids = [.. changed.Select(product => product.Id)];
    Guid[] existingIds = await context.Products.AsNoTracking()
      .Where(product => ids.Contains(product.Id))
      .Select(product => product.Id)
      .ToArrayAsync(cancellationToken);
    HashSet<Guid> existing = [.. existingIds];

    await InsertProductsAsync(
      [.. changed.Where(product => !existing.Contains(product.Id))],
      cancellationToken);
    await InsertFormatsAsync(changed, cancellationToken);
    await InsertSnapshotsAsync(changed, cancellationToken);
    await transaction.CommitAsync(cancellationToken);
  }

  private async Task InsertProductsAsync(
    ProductPrimitives[] products, CancellationToken cancellationToken
  ) {
    if (products.Length == 0) {
      return;
    }

    Guid[] ids = [.. products.Select(product => product.Id)];
    string[] names = [.. products.Select(product => product.Name)];
    Guid[] marketIds = [.. products.Select(product => product.MarketId)];
    Guid[] brandIds = [.. products.Select(product => product.BrandId)];
    await context.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO market.products (id, name, super_market_id, brand_id)
      SELECT * FROM unnest({ids}, {names}, {marketIds}, {brandIds});
      """, cancellationToken);
  }

  private async Task InsertFormatsAsync(
    ProductPrimitives[] products, CancellationToken cancellationToken
  ) {
    (ProductPrimitives Product, ProductFormatPrimitives Format)[] formats = [..
      products.SelectMany(product => product.Formats
        .Where(format => product.PendingFormatIds.Contains(format.Id))
        .Select(format => (Product: product, Format: format)))
    ];
    if (formats.Length == 0) {
      return;
    }

    Guid[] ids = [.. formats.Select(value => value.Format.Id)];
    Guid[] productIds = [.. formats.Select(value => value.Product.Id)];
    decimal[] quantities = [.. formats.Select(value => value.Format.Quantity)];
    string[] units = [.. formats.Select(value => value.Format.UnitOfMeasure)];
    string[] images = [.. formats.Select(value => value.Format.ImageUrl ?? string.Empty)];

    await context.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO market.product_formats (id, product_id, quantity, unit_of_measure, image_url)
      SELECT * FROM unnest({ids}, {productIds}, {quantities}, {units}, {images});
      """, cancellationToken);
  }

  private async Task InsertSnapshotsAsync(
    ProductPrimitives[] products, CancellationToken cancellationToken
  ) {
    (Guid FormatId, PriceSnapshotPrimitives Snapshot)[] snapshots = [..
      products.SelectMany(product => product.Formats)
        .SelectMany(format => format.PendingSnapshots.Select(snapshot =>
          (format.Id, snapshot)))
    ];
    if (snapshots.Length == 0) {
      return;
    }

    Guid[] ids = [.. snapshots.Select(value => value.Snapshot.Id)];
    Guid[] formatIds = [.. snapshots.Select(value => value.FormatId)];
    decimal[] prices = [.. snapshots.Select(value => value.Snapshot.Price)];
    DateOnly[] dates = [.. snapshots.Select(value => value.Snapshot.ObservedOn)];

    await context.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO market.price_snapshots (id, product_format_id, price_amount, observed_on)
      SELECT * FROM unnest({ids}, {formatIds}, {prices}, {dates});
      """, cancellationToken);
  }
}