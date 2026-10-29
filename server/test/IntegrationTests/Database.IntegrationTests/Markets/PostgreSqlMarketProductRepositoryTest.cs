using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Application.Markets;
using Metaspesa.Database.Entities;
using Metaspesa.Database.Repositories;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Metaspesa.Database.IntegrationTests.Markets;

[Collection("Database")]
public class PostgreSqlMarketProductRepositoryTests : IAsyncLifetime {
  private readonly DatabaseFixture _fixture;
  private readonly MainContext _context;
  private readonly PostgreSqlMarketProductRepository _productRepository;
  private readonly PostgreSqlPriceSnapshotRepository _snapshotRepository;

  public PostgreSqlMarketProductRepositoryTests(DatabaseFixture fixture) {
    _fixture = fixture;
    _context = fixture.CreateContext();
    _productRepository = new PostgreSqlMarketProductRepository(_context);
    _snapshotRepository = new PostgreSqlPriceSnapshotRepository(_context);
  }

  public async ValueTask InitializeAsync() {
    CancellationToken cancellationToken = TestContext.Current.CancellationToken;
    await _fixture.DeleteShoppingProductReferencesAsync(cancellationToken);
    await _context.PriceSnapshots.ExecuteDeleteAsync(cancellationToken);
    await _context.ProductFormats.ExecuteDeleteAsync(cancellationToken);
    await _context.Products.ExecuteDeleteAsync(cancellationToken);
    await _context.ProductBrands.ExecuteDeleteAsync(cancellationToken);
    await _context.SuperMarkets.ExecuteDeleteAsync(cancellationToken);
  }

  public async ValueTask DisposeAsync() {
    await _context.DisposeAsync();
    GC.SuppressFinalize(this);
  }

  [Fact(DisplayName = "Resolves products before appending snapshots")]
  public async Task Repository_ResolvesProducts_SeparatelyFromSnapshots() {
    MarketImport market = await CreateMarketImportAsync();
    DateTime observedAt = UtcDate(2026, 7, 28);

    ProductImportResult result = await _productRepository.ResolveProductsAsync(
      market, observedAt, TestContext.Current.CancellationToken);

    ProductId productId = Assert.Single(result.AddedProductIds);
    Assert.Single(result.AddedProductFormatIds);
    PriceObservation observation = Assert.Single(result.PriceObservations);
    Assert.Equal(new Money(1.99m), observation.Price);
    Assert.Equal(observedAt, observation.ObservedAt.Value);
    Assert.Equal(0, await _context.PriceSnapshots.CountAsync(
      TestContext.Current.CancellationToken));
    Assert.True(await _context.Products.AnyAsync(
      product => product.Id == productId.Value,
      TestContext.Current.CancellationToken));

    await _snapshotRepository.AppendAsync(
      result.PriceObservations,
      TestContext.Current.CancellationToken);

    PriceSnapshotDbEntity snapshot = await _context.PriceSnapshots
      .AsNoTracking()
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal(observation.ProductFormatId.Value, snapshot.ProductFormatId);
    Assert.Equal(1.99m, snapshot.PriceAmount);
    Assert.Equal("EUR", snapshot.CurrencyCode);
    Assert.Equal(observedAt, snapshot.ObservedAt);
  }

  [Fact(DisplayName = "Reuses matching product and format on repeated import")]
  public async Task Repository_ReusesProductAndFormat_WhenIdentityIsUnchanged() {
    MarketImport market = await CreateMarketImportAsync();
    ProductImportResult first = await _productRepository.ResolveProductsAsync(
      market, UtcDate(2026, 7, 28), TestContext.Current.CancellationToken);

    ProductImportResult second = await _productRepository.ResolveProductsAsync(
      market, UtcDate(2026, 7, 29), TestContext.Current.CancellationToken);

    Assert.Empty(second.AddedProductIds);
    Assert.Empty(second.AddedProductFormatIds);
    Assert.Equal(
      Assert.Single(first.PriceObservations).ProductFormatId,
      Assert.Single(second.PriceObservations).ProductFormatId);
    Assert.Equal(1, await _context.Products.CountAsync(
      TestContext.Current.CancellationToken));
    Assert.Equal(1, await _context.ProductFormats.CountAsync(
      TestContext.Current.CancellationToken));
  }

  [Fact(DisplayName = "Loads product aggregate with latest snapshots and no price history")]
  public async Task Repository_LoadsProduct_WithLatestPriceSnapshots() {
    ProductImportResult result = await ResolveAndAppendAsync();

    Product? product = await _productRepository.GetByIdAsync(
      Assert.Single(result.AddedProductIds),
      TestContext.Current.CancellationToken);

    Assert.NotNull(product);
    Assert.Equal(new ProductName("Milk"), product.Name);
    Assert.NotEqual(Guid.Empty, product.BrandId.Value);
    ProductFormat format = Assert.Single(product.Formats);
    Assert.Equal(Quantity.Create(1, "ml"), format.Quantity);
    Assert.DoesNotContain(
      typeof(Product).GetProperties(),
      property => property.PropertyType == typeof(PriceSnapshot));
    Assert.Equal(1.99m, format.LatestSnapshot.Price.Amount);
    Assert.Empty(product.PendingSnapshots);
  }

  [Fact(DisplayName = "Catalog excludes products without price history")]
  public async Task Repository_ExcludesProductWithoutSnapshot_FromCatalog() {
    MarketImport market = await CreateMarketImportAsync();
    await _productRepository.ResolveProductsAsync(
      market, UtcDate(2026, 7, 28), TestContext.Current.CancellationToken);

    PagedResult<CatalogProduct> result = await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(null, [], null, new Pagination(1, 24)),
      TestContext.Current.CancellationToken);

    Assert.Empty(result.Values);
    Assert.Equal(0, result.TotalCount);
  }

  [Fact(DisplayName = "Catalog returns latest snapshot without adding it to product")]
  public async Task Repository_ProjectsLatestSnapshot_InCatalog() {
    ProductImportResult result = await ResolveAndAppendAsync();
    ProductFormatId formatId = Assert.Single(result.PriceObservations).ProductFormatId;
    await _snapshotRepository.AppendAsync(
      [new PriceObservation(
        formatId,
        new Money(2.49m),
        UtcDate(2026, 7, 29))],
      TestContext.Current.CancellationToken);

    PagedResult<CatalogProduct> resultPage =
      await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(null, [], null, new Pagination(1, 24)),
      TestContext.Current.CancellationToken);

    CatalogProduct product = Assert.Single(resultPage.Values);
    CatalogFormat format = Assert.Single(product.Formats);
    Assert.Equal("Mercadona", product.Market.Name);
    Assert.Equal("Milk", product.Name);
    Assert.Equal("Brand", product.Brand);
    Assert.Equal(2.49m, format.Price);
    Assert.Equal(1, resultPage.TotalCount);
  }

  [Fact(DisplayName = "Catalog projects the persisted product format identifier")]
  public async Task Repository_ProjectsProductFormatIdentifier_InCatalog() {
    // Arrange
    ProductImportResult import = await ResolveAndAppendAsync();
    ProductFormatId persistedFormatId =
      Assert.Single(import.PriceObservations).ProductFormatId;

    // Act
    PagedResult<CatalogProduct> result = await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(null, [], null, new Pagination(1, 24)),
      TestContext.Current.CancellationToken);

    // Assert
    CatalogProduct product = Assert.Single(result.Values);
    Assert.Equal(
      persistedFormatId.Value,
      Assert.Single(product.Formats).Id);
  }

  [Fact(DisplayName = "Catalog applies market, brand, name, and pagination filters")]
  public async Task Repository_AppliesCatalogFilters_AndPagination() {
    await ResolveAndAppendAsync();
    Guid marketId = await _context.SuperMarkets.Select(market => market.Id)
      .SingleAsync(TestContext.Current.CancellationToken);

    PagedResult<CatalogProduct> matching =
      await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(
        "Mil",
        [new MarketId(marketId)],
        "ran",
        new Pagination(1, 1)),
      TestContext.Current.CancellationToken);
    PagedResult<CatalogProduct> missing =
      await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(
        null,
        [new MarketId(Guid.Parse("00000000-0000-7000-8000-00007fffffff"))],
        null,
        new Pagination(1, 24)),
      TestContext.Current.CancellationToken);

    Assert.Single(matching.Values);
    Assert.Equal(1, matching.TotalCount);
    Assert.Empty(missing.Values);
    Assert.Equal(0, missing.TotalCount);
  }

  [Fact(DisplayName = "Catalog pagination preserves total and returns requested page")]
  public async Task Repository_PaginatesProducts_AndPreservesTotalCount() {
    await ResolveAndAppendAsync();
    var secondImport = new MarketImport(
      new MarketName("Mercadona"),
      [new ProductImport(
        new ProductName("Yogurt"),
        new BrandName("Brand"),
        [new ProductFormatImport(
          Quantity.Create(1, "g"),
          new Money(2.49m),
          null)])]);
    ProductImportResult second = await _productRepository.ResolveProductsAsync(
      secondImport, UtcDate(2026, 7, 28), TestContext.Current.CancellationToken);
    await _snapshotRepository.AppendAsync(
      second.PriceObservations, TestContext.Current.CancellationToken);

    PagedResult<CatalogProduct> result = await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(null, [], null, new Pagination(2, 1)),
      TestContext.Current.CancellationToken);

    Assert.Equal(2, result.TotalCount);
    CatalogProduct product = Assert.Single(result.Values);
    Assert.Equal("Yogurt", product.Name);
  }

  [Fact(DisplayName = "Repository filters by market and sorts by latest format price")]
  public async Task Repository_FiltersByMarketAndSortsByLatestFormatPrice() {
    ProductImportResult milk = await ResolveAndAppendAsync();
    ProductFormatId milkFormatId = Assert.Single(milk.PriceObservations).ProductFormatId;
    await _snapshotRepository.AppendAsync(
      [new PriceObservation(milkFormatId, new Money(3.00m), UtcDate(2026, 7, 29))],
      TestContext.Current.CancellationToken);
    var secondImport = new MarketImport(
      new MarketName("Mercadona"),
      [new ProductImport(new ProductName("Yogurt"), new BrandName("Brand"),
        [new ProductFormatImport(
          Quantity.Create(1, "g"), new Money(2.00m), null)])]);
    ProductImportResult yogurt = await _productRepository.ResolveProductsAsync(
      secondImport, UtcDate(2026, 7, 28), TestContext.Current.CancellationToken);
    await _snapshotRepository.AppendAsync(
      yogurt.PriceObservations, TestContext.Current.CancellationToken);
    Guid marketId = await _context.SuperMarkets.Select(market => market.Id)
      .SingleAsync(TestContext.Current.CancellationToken);
    PagedResult<CatalogProduct> result = await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(null, [new MarketId(marketId)], null,
        new Pagination(1, 1),
        CatalogSort.PriceAsc), TestContext.Current.CancellationToken);
    PagedResult<CatalogProduct> missing = await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(null, [new MarketId(Guid.Parse("00000000-0000-7000-8000-00007fffffff"))], null,
        new Pagination(1, 24),
        CatalogSort.Name), TestContext.Current.CancellationToken);

    Assert.Equal(2, result.TotalCount);
    CatalogProduct product = Assert.Single(result.Values);
    Assert.Equal("Yogurt", product.Name);
    Assert.Equal(marketId, product.Market.Id);
    Assert.Equal(2.00m, Assert.Single(product.Formats).Price);
    Assert.Empty(missing.Values);
    Assert.Equal(0, missing.TotalCount);
  }

  [Fact(DisplayName = "Confirms all requested product formats exist")]
  public async Task CheckFormatsExistsAsync_ReturnsTrue_WhenAllFormatsExist() {
    ProductImportResult import = await ResolveAndAppendAsync();
    Guid existingId = Assert.Single(import.PriceObservations)
      .ProductFormatId.Value;

    bool result = await _productRepository.CheckFormatsExistAsync(
      [existingId], TestContext.Current.CancellationToken);

    Assert.True(result);
  }

  [Fact(DisplayName = "Rejects requested product formats when one is missing")]
  public async Task CheckFormatsExistsAsync_ReturnsFalse_WhenAnyFormatIsMissing() {
    ProductImportResult import = await ResolveAndAppendAsync();
    Guid existingId = Assert.Single(import.PriceObservations)
      .ProductFormatId.Value;

    bool result = await _productRepository.CheckFormatsExistAsync(
      [existingId, Guid.CreateVersion7()],
      TestContext.Current.CancellationToken);

    Assert.False(result);
  }

  [Fact(DisplayName = "Accepts duplicate references to an existing format")]
  public async Task CheckFormatsExistsAsync_ReturnsTrue_WhenFormatIdsRepeat() {
    ProductImportResult import = await ResolveAndAppendAsync();
    Guid existingId = Assert.Single(import.PriceObservations)
      .ProductFormatId.Value;

    bool result = await _productRepository.CheckFormatsExistAsync(
      [existingId, existingId], TestContext.Current.CancellationToken);

    Assert.True(result);
  }

  [Fact(DisplayName = "Accepts an empty product format set")]
  public async Task CheckFormatsExistsAsync_ReturnsTrue_WhenNoFormatsRequested() {
    bool result = await _productRepository.CheckFormatsExistAsync(
      [], TestContext.Current.CancellationToken);

    Assert.True(result);
  }

  [Fact(DisplayName = "Deletes product formats and snapshots created by import")]
  public async Task Repository_DeletesProductGraph_ByTypedProductId() {
    ProductImportResult result = await ResolveAndAppendAsync();

    await _productRepository.DeleteProductsAsync(
      result.AddedProductIds,
      TestContext.Current.CancellationToken);

    Assert.Equal(0, await _context.Products.CountAsync(
      TestContext.Current.CancellationToken));
    Assert.Equal(0, await _context.ProductFormats.CountAsync(
      TestContext.Current.CancellationToken));
    Assert.Equal(0, await _context.PriceSnapshots.CountAsync(
      TestContext.Current.CancellationToken));
  }

  [Fact(DisplayName = "Deletes new format without deleting existing product")]
  public async Task Repository_DeletesNewFormat_WithoutDeletingExistingProduct() {
    await ResolveAndAppendAsync();
    var secondImport = new MarketImport(
      new MarketName("Mercadona"),
      [new ProductImport(
        new ProductName("Milk"),
        new BrandName("Brand"),
        [new ProductFormatImport(
          Quantity.Create(2, "g"),
          new Money(3.49m),
          null)])]);
    ProductImportResult secondResult =
      await _productRepository.ResolveProductsAsync(
      secondImport,
      UtcDate(2026, 7, 29),
      TestContext.Current.CancellationToken);

    Assert.Empty(secondResult.AddedProductIds);
    ProductFormatId addedFormatId = Assert.Single(
      secondResult.AddedProductFormatIds);
    await _productRepository.DeleteProductFormatsAsync(
      [addedFormatId],
      TestContext.Current.CancellationToken);

    Assert.Equal(1, await _context.Products.CountAsync(
      TestContext.Current.CancellationToken));
    Assert.Equal(1, await _context.ProductFormats.CountAsync(
      TestContext.Current.CancellationToken));
  }

  private async Task<ProductImportResult> ResolveAndAppendAsync() {
    MarketImport market = await CreateMarketImportAsync();
    ProductImportResult result =
      await _productRepository.ResolveProductsAsync(
      market,
      UtcDate(2026, 7, 28),
      TestContext.Current.CancellationToken);
    await _snapshotRepository.AppendAsync(
      result.PriceObservations,
      TestContext.Current.CancellationToken);
    return result;
  }

  [Theory]
  [InlineData("kg")]
  [InlineData("ML")]
  [InlineData("box")]
  [InlineData("")]
  public async Task Database_RejectsNonCanonicalUnitCode(string code) {
    await ResolveAndAppendAsync();

    Task action() => _context.Database.ExecuteSqlInterpolatedAsync(
      $"UPDATE market.product_formats SET unit_of_measure = {code}",
      TestContext.Current.CancellationToken);

    PostgresException exception = await Assert.ThrowsAsync<PostgresException>(action);
    Assert.Equal("chk_product_format_unit_of_measure", exception.ConstraintName);
  }

  [Fact]
  public async Task ResolveProductsAsync_RejectsUnknownMarket() {
    // Arrange
    MarketImport import = CreateReferenceImport();

    // Act
    Exception? failure = await Record.ExceptionAsync(() => _productRepository.ResolveProductsAsync(
      import, DateTime.UnixEpoch, TestContext.Current.CancellationToken));

    // Assert
    Assert.IsType<UnknownMarketException>(failure);
  }

  [Fact]
  public async Task ResolveProductsAsync_UnknownMarketDoesNotWriteProducts() {
    // Arrange
    MarketImport import = CreateReferenceImport();

    // Act
    await Record.ExceptionAsync(() => _productRepository.ResolveProductsAsync(
      import, DateTime.UnixEpoch, TestContext.Current.CancellationToken));

    // Assert
    Assert.Empty(await _context.Products.ToListAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task ResolveProductsAsync_RejectsUnknownBrand() {
    // Arrange
    await SeedImportReferencesAsync(includeBrand: false);
    MarketImport import = CreateReferenceImport();

    // Act
    Exception? failure = await Record.ExceptionAsync(() =>
      _productRepository.ResolveProductsAsync(import, DateTime.UnixEpoch, TestContext.Current.CancellationToken));

    // Assert
    Assert.IsType<UnknownProductBrandException>(failure);
  }

  [Fact]
  public async Task ResolveProductsAsync_UnknownBrandDoesNotWriteProducts() {
    // Arrange
    await SeedImportReferencesAsync(includeBrand: false);
    MarketImport import = CreateReferenceImport();

    // Act
    await Record.ExceptionAsync(() =>
      _productRepository.ResolveProductsAsync(import, DateTime.UnixEpoch, TestContext.Current.CancellationToken));

    // Assert
    Assert.Empty(await _context.Products.ToListAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task ResolveProductsAsync_ReusesFormatForEquivalentUnitsAndReferenceNames() {
    // Arrange
    await SeedImportReferencesAsync();
    MarketImport first = CreateReferenceImport("market", "BRAND", 1, "l");
    await _productRepository.ResolveProductsAsync(first, UtcDate(2026, 10, 25), TestContext.Current.CancellationToken);
    Guid originalId = await _context.ProductFormats.Select(value => value.Id).SingleAsync(TestContext.Current.CancellationToken);
    MarketImport equivalent = CreateReferenceImport("MARKET", "brand", 1000, "ml");

    // Act
    await _productRepository.ResolveProductsAsync(equivalent, UtcDate(2026, 10, 25), TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(originalId, await _context.ProductFormats.Select(value => value.Id).SingleAsync(TestContext.Current.CancellationToken));
  }

  private async Task SeedImportReferencesAsync(bool includeBrand = true) {
    _context.SuperMarkets.Add(new SuperMarketDbEntity {
      Id = Guid.CreateVersion7(), Name = "Market",
    });
    if (includeBrand) {
      _context.ProductBrands.Add(new ProductBrandDbEntity {
        Id = Guid.CreateVersion7(), Name = "Brand",
      });
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
  }

  private static MarketImport CreateReferenceImport(
    string marketName = "Market", string brandName = "Brand",
    decimal quantity = 1, string unit = "l"
  ) {
    var format = new ProductFormatImport(Quantity.Create(quantity, unit), new Money(1), null);
    var product = new ProductImport(new ProductName("Milk"), new BrandName(brandName), [format]);
    return new MarketImport(new MarketName(marketName), [product]);
  }

  private async Task<MarketImport> CreateMarketImportAsync() {
    _context.SuperMarkets.Add(new SuperMarketDbEntity {
      Id = Uid.Create(),
      Name = "Mercadona",
    });
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

    await SeedBrandsAsync(
      [new BrandName("Brand")],
      TestContext.Current.CancellationToken);

    return new MarketImport(
      new MarketName("Mercadona"),
      [new ProductImport(
        new ProductName("Milk"),
        new BrandName("Brand"),
        [new ProductFormatImport(
          Quantity.Create(1, "ml"),
          new Money(1.99m),
          new ImageUrl(new Uri("https://example.com/milk.png")))])]);
  }

  private async Task SeedBrandsAsync(IReadOnlyCollection<BrandName> brands, CancellationToken cancellationToken) {
    _context.ProductBrands.AddRange(brands.Select(brand => new ProductBrandDbEntity {
      Id = Uid.Create(), Name = brand.Value,
    }));
    await _context.SaveChangesAsync(cancellationToken);
  }
  private static DateTime UtcDate(int year, int month, int day) =>
    new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}