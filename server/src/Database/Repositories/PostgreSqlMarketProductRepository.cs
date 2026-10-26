using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Database.Entities;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Metaspesa.Database.Repositories;

internal partial class PostgreSqlMarketProductRepository(
  MainContext context
) : IProductRepository {
  private const int BatchSize = 1_000;

  private readonly record struct ProductSource(
    Guid ProductId, ProductImport Source);
  private readonly record struct ProductInsert(
    ProductDbEntity Entity, ProductImport Source);
  private readonly record struct ProductFormatKey(
    Guid ProductId,
    decimal Quantity,
    string UnitOfMeasure
  );

  public async Task<Product?> GetByIdAsync(
    ProductId productId, CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync(async () => {
    ProductDbEntity? entity = await context.Products
      .AsNoTracking()
      .Include(product => product.Brand)
      .Include(product => product.Formats)
      .SingleOrDefaultAsync(
        product => product.Id == productId.Value,
        cancellationToken);

    return entity?.MapToDomain();
  }, "Couldn't get product.");

  public async Task<PagedResult<CatalogProduct>> GetProductsAsync(
    GetMarketProductsFilter filter, CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync(async () => {
    IQueryable<ProductDbEntity> query = context.Products
      .AsNoTracking()
      .Where(product => product.Formats.Any(format => format.PriceSnapshots.Count > 0));

    if (!string.IsNullOrWhiteSpace(filter.NameSegment)) {
      query = query.Where(product => EF.Functions.ILike(
        product.Name, $"%{EscapeLike(filter.NameSegment)}%", "\\"));
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
          .OrderByDescending(snapshot => snapshot.ObservedAt)
          .ThenByDescending(snapshot => snapshot.Id)
          .Select(snapshot => snapshot.PriceAmount).First())),
      CatalogSort.PriceDesc => query.OrderByDescending(product => product.Formats
        .Where(format => format.PriceSnapshots.Count > 0)
        .Min(format => format.PriceSnapshots
          .OrderByDescending(snapshot => snapshot.ObservedAt)
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

  public async Task<ProductImportResult> ResolveProductsAsync(
    MarketImport market,
    DateTime observedAt,
    CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync(async () => {
    bool autoDetectChangesEnabled =
      context.ChangeTracker.AutoDetectChangesEnabled;
    context.ChangeTracker.AutoDetectChangesEnabled = false;
    try {
      context.ChangeTracker.Clear();

      Guid marketId = await GetMarketIdAsync(market.Name.Value, cancellationToken);
      Dictionary<string, Guid> brandLookup = await GetBrandLookupAsync(
        market, cancellationToken);
      Dictionary<(string Name, Guid BrandId), Guid> existingProducts =
        await GetExistingProductLookupAsync(marketId, cancellationToken);

      (List<ProductSource> productSources, List<ProductId> addedProductIds) =
        await ResolveProductSourcesAsync(
          market.Products,
          marketId,
          brandLookup,
          existingProducts,
          cancellationToken);

      (
        Dictionary<ProductFormatKey, Guid> formatLookup,
        IReadOnlyCollection<ProductFormatId> addedProductFormatIds
      ) = await ResolveProductFormatsAsync(
        productSources, cancellationToken);
      IReadOnlyCollection<PriceObservation> observations = BuildPriceObservations(
        productSources, formatLookup, observedAt);

      return new ProductImportResult(
        addedProductIds, addedProductFormatIds, observations);
    } finally {
      context.ChangeTracker.AutoDetectChangesEnabled =
        autoDetectChangesEnabled;
    }
  }, "Couldn't resolve market products.");

  public async Task DeleteProductsAsync(
    IReadOnlyCollection<ProductId> productIds,
    CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync(
    async () => await DeleteProductsByIdsAsync(
      [.. productIds.Select(id => id.Value)], cancellationToken),
    "Couldn't delete products.");

  public async Task DeleteProductFormatsAsync(
    IReadOnlyCollection<ProductFormatId> productFormatIds,
    CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync(async () => {
    Guid[] ids = [.. productFormatIds.Select(id => id.Value)];
    await context.PriceSnapshots
      .Where(snapshot => ids.Contains(snapshot.ProductFormatId))
      .ExecuteDeleteAsync(cancellationToken);
    await context.ProductFormats
      .Where(format => ids.Contains(format.Id))
      .ExecuteDeleteAsync(cancellationToken);
  }, "Couldn't delete product formats.");

  private static CatalogFormat ToCatalogFormat(ProductFormatDbEntity format) {
    PriceSnapshotDbEntity snapshot = format.PriceSnapshots
      .OrderByDescending(value => value.ObservedAt)
      .ThenByDescending(value => value.Id)
      .First();
    return new CatalogFormat(
      format.Id, format.Quantity, format.UnitOfMeasure,
      snapshot.PriceAmount, snapshot.CurrencyCode, ToUri(format.ImageUrl),
      DateTime.SpecifyKind(snapshot.ObservedAt, DateTimeKind.Utc));
  }

  private async Task<Guid> GetMarketIdAsync(
    string marketName,
    CancellationToken cancellationToken
  ) {
    Guid? id = await context.SuperMarkets.AsNoTracking()
      .Where(market => market.Name == marketName)
      .Select(market => (Guid?)market.Id)
      .SingleOrDefaultAsync(cancellationToken);
    return id ?? throw new UnknownMarketException(new MarketName(marketName));
  }

  private async Task<Dictionary<string, Guid>> GetBrandLookupAsync(
    MarketImport market,
    CancellationToken cancellationToken
  ) {
    string[] names = [.. market.Products.Select(product => product.Brand.Value)
      .Distinct(StringComparer.InvariantCultureIgnoreCase)];
    Dictionary<string, Guid> brands = await context.ProductBrands.AsNoTracking()
      .Where(brand => names.Contains(brand.Name))
      .ToDictionaryAsync(brand => brand.Name, brand => brand.Id,
        StringComparer.InvariantCultureIgnoreCase, cancellationToken);
    ProductImport? unknownProduct = market.Products.FirstOrDefault(product =>
      !brands.ContainsKey(product.Brand.Value));
    if (unknownProduct is not null) {
      throw new UnknownProductBrandException(
        market.Name, unknownProduct.Name, unknownProduct.Brand);
    }
    return brands;
  }

  private async Task<Dictionary<(string Name, Guid BrandId), Guid>>
    GetExistingProductLookupAsync(
      Guid marketId,
      CancellationToken cancellationToken
    ) => await context.Products
      .Where(product => product.SuperMarketId == marketId)
      .ToDictionaryAsync(
        product => new ValueTuple<string, Guid>(product.Name, product.BrandId),
        product => product.Id,
        cancellationToken);

  private async Task<(List<ProductSource> Sources, List<ProductId> AddedProductIds)>
    ResolveProductSourcesAsync(
      IReadOnlyCollection<ProductImport> products,
      Guid marketId,
      Dictionary<string, Guid> brandLookup,
      Dictionary<(string Name, Guid BrandId), Guid> existingProducts,
      CancellationToken cancellationToken
    ) {
    var toInsert = new List<ProductInsert>();
    var sources = new List<ProductSource>();

    foreach (ProductImport product in products) {
      Guid brandId = brandLookup[product.Brand.Value];
      if (existingProducts.TryGetValue(
        (product.Name.Value, brandId), out Guid existingId)) {
        sources.Add(new ProductSource(existingId, product));
        continue;
      }

      toInsert.Add(new ProductInsert(
        new ProductDbEntity {
          Id = Uid.Create(),
          Name = product.Name.Value,
          SuperMarketId = marketId,
          BrandId = brandId,
        },
        product));
    }

    List<ProductSource> newSources = await AddProductsAsync(
      toInsert, cancellationToken);
    sources.AddRange(newSources);

    return (
      sources,
      [.. newSources.Select(source => new ProductId(source.ProductId))]);
  }

  private async Task<List<ProductSource>> AddProductsAsync(
    IReadOnlyCollection<ProductInsert> toInsert,
    CancellationToken cancellationToken
  ) {
    IEnumerable<List<ProductInsert>> batches = toInsert
      .Chunk(BatchSize)
      .Select(batch => batch.ToList());

    List<ProductSource> sources = [];
    foreach (List<ProductInsert> batch in batches) {
      await context.Products.AddRangeAsync(
        batch.Select(value => value.Entity), cancellationToken);
      await context.SaveChangesAsync(cancellationToken);
      context.ChangeTracker.Clear();
      sources.AddRange(batch.Select(value =>
        new ProductSource(value.Entity.Id, value.Source)));
    }

    return sources;
  }

  private async Task<(
    Dictionary<ProductFormatKey, Guid> FormatLookup,
    IReadOnlyCollection<ProductFormatId> AddedProductFormatIds
  )> ResolveProductFormatsAsync(
    IReadOnlyCollection<ProductSource> productSources,
    CancellationToken cancellationToken
  ) {
    Dictionary<ProductFormatKey, Guid> formatLookup =
      await GetExistingProductFormatLookupAsync(
        productSources, cancellationToken);
    List<ProductFormatDbEntity> missingFormats = BuildMissingProductFormats(
      productSources, formatLookup);
    IDictionary<ProductFormatKey, Guid> newFormats = await AddProductFormatsAsync(
      missingFormats, cancellationToken);

    return (
      formatLookup.Concat(newFormats)
        .ToDictionary(pair => pair.Key, pair => pair.Value),
      [.. newFormats.Values.Select(id => new ProductFormatId(id))]);
  }

  private async Task<Dictionary<ProductFormatKey, Guid>>
    GetExistingProductFormatLookupAsync(
      IReadOnlyCollection<ProductSource> productSources,
      CancellationToken cancellationToken
    ) {
    Guid[] productIds = [
      ..productSources.Select(source => source.ProductId).Distinct()
    ];

    return await context.ProductFormats
      .Where(format => productIds.Contains(format.ProductId))
      .Select(format => new {
        format.Id,
        format.ProductId,
        format.Quantity,
        format.UnitOfMeasure,
      })
      .ToDictionaryAsync(
        format => new ProductFormatKey(
          format.ProductId,
          format.Quantity,
          format.UnitOfMeasure),
        format => format.Id,
        cancellationToken);
  }

  private static List<ProductFormatDbEntity> BuildMissingProductFormats(
    IEnumerable<ProductSource> productSources,
    IReadOnlyDictionary<ProductFormatKey, Guid> existingFormatLookup
  ) {
    var missingFormats = new Dictionary<ProductFormatKey, ProductFormatDbEntity>();

    foreach (ProductSource productSource in productSources) {
      foreach (ProductFormatImport format in productSource.Source.Formats) {
        ProductFormatKey key = ToProductFormatKey(
          productSource.ProductId, format);
        if (existingFormatLookup.ContainsKey(key) ||
          missingFormats.ContainsKey(key)) {
          continue;
        }

        missingFormats[key] = new ProductFormatDbEntity {
          Id = Uid.Create(),
          ProductId = key.ProductId,
          Quantity = key.Quantity,
          UnitOfMeasure = key.UnitOfMeasure,
          ImageUrl = format.ImageUrl?.Value.ToString() ?? string.Empty,
        };
      }
    }

    return [.. missingFormats.Values];
  }

  private async Task<IDictionary<ProductFormatKey, Guid>> AddProductFormatsAsync(
    IReadOnlyCollection<ProductFormatDbEntity> formats,
    CancellationToken cancellationToken
  ) {
    var newFormatLookup = new Dictionary<ProductFormatKey, Guid>();
    IEnumerable<List<ProductFormatDbEntity>> batches = formats
      .Chunk(BatchSize)
      .Select(batch => batch.ToList());

    foreach (List<ProductFormatDbEntity> batch in batches) {
      await context.ProductFormats.AddRangeAsync(batch, cancellationToken);
      await context.SaveChangesAsync(cancellationToken);
      context.ChangeTracker.Clear();

      foreach (ProductFormatDbEntity format in batch) {
        var key = new ProductFormatKey(
          format.ProductId,
          format.Quantity,
          format.UnitOfMeasure);
        newFormatLookup[key] = format.Id;
      }
    }

    return newFormatLookup;
  }

  private static IReadOnlyCollection<PriceObservation> BuildPriceObservations(
    IEnumerable<ProductSource> productSources,
    IReadOnlyDictionary<ProductFormatKey, Guid> formatLookup,
    DateTime observedAt
  ) => [
    ..productSources.SelectMany(productSource =>
      productSource.Source.Formats.Select(format => {
        ProductFormatKey key = ToProductFormatKey(
          productSource.ProductId, format);
        return new PriceObservation(
          new ProductFormatId(formatLookup[key]),
          format.Price,
          observedAt);
      }))
  ];

  private static ProductFormatKey ToProductFormatKey(
    Guid productId,
    ProductFormatImport format
  ) => new(
    productId,
    format.Quantity.Amount,
    format.Quantity.UnitOfMeasure.Value);

  private async Task DeleteProductsByIdsAsync(
    IReadOnlyCollection<Guid> productIds,
    CancellationToken cancellationToken
  ) {
    await context.PriceSnapshots
      .Where(snapshot => productIds.Contains(snapshot.ProductFormat.ProductId))
      .ExecuteDeleteAsync(cancellationToken);
    await context.ProductFormats
      .Where(format => productIds.Contains(format.ProductId))
      .ExecuteDeleteAsync(cancellationToken);
    await context.Products
      .Where(product => productIds.Contains(product.Id))
      .ExecuteDeleteAsync(cancellationToken);
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
}