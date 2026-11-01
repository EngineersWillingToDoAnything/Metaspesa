using System.Data.Common;
using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Application.Markets;
using Metaspesa.Database.Entities;
using Metaspesa.Database.Repositories;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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

  [Fact]
  public async Task SaveBatchAsync_PreservesProductId() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);

    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Guid actual = await _context.Products.Select(value => value.Id).SingleAsync(TestContext.Current.CancellationToken);

    Assert.Equal(product.Id.Value, actual);
  }

  [Fact]
  public async Task SaveBatchAsync_PreservesFormatId() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);

    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Guid actual = await _context.ProductFormats.Select(value => value.Id).SingleAsync(TestContext.Current.CancellationToken);

    Assert.Equal(product.Formats.Single().Id.Value, actual);
  }

  [Fact]
  public async Task SaveBatchAsync_PreservesSnapshotId() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);

    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Guid actual = await _context.PriceSnapshots.Select(value => value.Id).SingleAsync(TestContext.Current.CancellationToken);

    Assert.Equal(product.PendingSnapshots.Single().Id.Value, actual);
  }

  [Fact]
  public async Task SaveBatchAsync_PersistsSnapshotPrice() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);

    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    decimal actual = await _context.PriceSnapshots.Select(value => value.PriceAmount).SingleAsync(TestContext.Current.CancellationToken);

    Assert.Equal(product.PendingSnapshots.Single().Price.Amount, actual);
  }

  [Fact]
  public async Task SaveBatchAsync_PersistsSnapshotTime() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);

    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    DateTime actual = await _context.PriceSnapshots.Select(value => value.ObservedAt).SingleAsync(TestContext.Current.CancellationToken);

    Assert.Equal(product.PendingSnapshots.Single().ObservedAt.Value, actual);
  }

  [Fact]
  public async Task SaveBatchAsync_DoesNotFlushUnrelatedTrackedChanges() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    brand.Name = "Unflushed";

    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    string actual = await _context.ProductBrands.AsNoTracking().Select(value => value.Name).SingleAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Brand", actual);
  }

  [Fact]
  public async Task SaveBatchAsync_AppendsChangedSnapshotWithoutRewritingHistory() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Product aggregate = (await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))!;
    Guid initialId = product.PendingSnapshots.Single().Id.Value;
    aggregate.RegisterObservation(observation with {
      ObservedAt = observation.ObservedAt.AddDays(1),
      Formats = [new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/new.png"))],
    });

    await _productRepository.SaveBatchAsync([aggregate], TestContext.Current.CancellationToken);
    Guid[] ids = await _context.PriceSnapshots.OrderBy(value => value.ObservedAt).Select(value => value.Id).ToArrayAsync(TestContext.Current.CancellationToken);

    Assert.Equal([initialId, aggregate.PendingSnapshots.Single().Id.Value], ids);
  }

  [Fact]
  public async Task SaveBatchAsync_DoesNotRewriteExistingProductName() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Product aggregate = (await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))!;
    aggregate.RegisterObservation(observation with {
      ObservedAt = observation.ObservedAt.AddDays(1),
      Formats = [new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/new.png"))],
    });

    await _productRepository.SaveBatchAsync([aggregate], TestContext.Current.CancellationToken);
    string actual = await _context.Products.Select(value => value.Name).SingleAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Milk", actual);
  }

  [Fact]
  public async Task SaveBatchAsync_DoesNotRewriteExistingFormatImage() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Product aggregate = (await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))!;
    aggregate.RegisterObservation(observation with {
      ObservedAt = observation.ObservedAt.AddDays(1),
      Formats = [new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/new.png"))],
    });

    await _productRepository.SaveBatchAsync([aggregate], TestContext.Current.CancellationToken);
    string actual = await _context.ProductFormats.Select(value => value.ImageUrl).SingleAsync(TestContext.Current.CancellationToken);

    Assert.Equal("https://example.com/milk.png", actual);
  }

  [Fact]
  public async Task SaveBatchAsync_InsertsOnlyNewFormat() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Product aggregate = (await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))!;
    aggregate.RegisterObservation(observation with {
      ObservedAt = observation.ObservedAt.AddDays(1),
      Formats = [new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png"))],
    });

    await _productRepository.SaveBatchAsync([aggregate], TestContext.Current.CancellationToken);
    Guid[] actual = await _context.ProductFormats.OrderBy(value => value.Id).Select(value => value.Id).ToArrayAsync(TestContext.Current.CancellationToken);

    Assert.Equal(aggregate.Formats.Select(value => value.Id.Value).Order(), actual);
  }

  [Fact]
  public async Task SaveBatchAsync_DoesNotExecuteCommandsForUnchangedObservation() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Product aggregate = (await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))!;
    aggregate.RegisterObservation(observation with { ObservedAt = observation.ObservedAt.AddDays(1) });
    var commands = new CommandCounter();
    await using MainContext observed = _fixture.CreateContext(commands);
    var repository = new PostgreSqlMarketProductRepository(observed);

    await repository.SaveBatchAsync([aggregate], TestContext.Current.CancellationToken);

    Assert.Equal(0, commands.Count);
  }

  [Fact]
  public async Task SaveBatchAsync_DoesNotExecuteCommandsForHydratedAggregate() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Product aggregate = (await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))!;
    var commands = new CommandCounter();
    await using MainContext observed = _fixture.CreateContext(commands);
    var repository = new PostgreSqlMarketProductRepository(observed);

    await repository.SaveBatchAsync([aggregate], TestContext.Current.CancellationToken);

    Assert.Equal(0, commands.Count);
  }

  [Fact]
  public async Task SaveBatchAsync_RejectsZeroPrice() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var valid = Product.Create(observation);
    var invalid = Product.Create(observation with {
      ProductName = "Invalid price",
      Formats = [new ProductFormatObservationParams(1, "l", 0, new Uri("https://example.com/invalid.png"))],
    });

    Task action() => _productRepository.SaveBatchAsync([valid, invalid], TestContext.Current.CancellationToken);

    PostgresException error = await Assert.ThrowsAsync<PostgresException>(action);
    Assert.Equal("chk_price_snapshot_positive_price", error.ConstraintName);
  }

  [Fact]
  public async Task SaveBatchAsync_RollsBackProducts_WhenSnapshotInsertFails() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var valid = Product.Create(observation);
    var invalid = Product.Create(observation with {
      ProductName = "Invalid price",
      Formats = [new ProductFormatObservationParams(1, "l", 0, new Uri("https://example.com/invalid.png"))],
    });

    _ = await Record.ExceptionAsync(() => _productRepository.SaveBatchAsync([valid, invalid], TestContext.Current.CancellationToken));
    int remaining = await _context.Products.CountAsync(TestContext.Current.CancellationToken);

    Assert.Equal(0, remaining);
  }

  [Fact]
  public async Task SaveBatchAsync_RollsBackProductFormats_WhenSnapshotInsertFails() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var valid = Product.Create(observation);
    var invalid = Product.Create(observation with {
      ProductName = "Invalid price",
      Formats = [new ProductFormatObservationParams(1, "l", 0, new Uri("https://example.com/invalid.png"))],
    });

    _ = await Record.ExceptionAsync(() => _productRepository.SaveBatchAsync([valid, invalid], TestContext.Current.CancellationToken));
    int remaining = await _context.ProductFormats.CountAsync(TestContext.Current.CancellationToken);

    Assert.Equal(0, remaining);
  }

  [Fact]
  public async Task SaveBatchAsync_RollsBackPriceSnapshots_WhenSnapshotInsertFails() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var valid = Product.Create(observation);
    var invalid = Product.Create(observation with {
      ProductName = "Invalid price",
      Formats = [new ProductFormatObservationParams(1, "l", 0, new Uri("https://example.com/invalid.png"))],
    });

    _ = await Record.ExceptionAsync(() => _productRepository.SaveBatchAsync([valid, invalid], TestContext.Current.CancellationToken));
    int remaining = await _context.PriceSnapshots.CountAsync(TestContext.Current.CancellationToken);

    Assert.Equal(0, remaining);
  }

  [Fact]
  public async Task SaveBatchAsync_RejectsDuplicateIdentity() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    var duplicate = Product.Create(observation with { ProductName = "MILK" });

    Task action() => _productRepository.SaveBatchAsync([duplicate], TestContext.Current.CancellationToken);

    PostgresException error = await Assert.ThrowsAsync<PostgresException>(action);
    Assert.Equal("idx_product_name_super_market_brand", error.ConstraintName);
  }

  [Fact]
  public async Task SaveBatchAsync_RejectsDuplicateAggregateInBatch() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);

    Task action() => _productRepository.SaveBatchAsync([product, product], TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<ArgumentException>(action);
  }

  [Fact]
  public async Task SaveBatchAsync_PreservesPurchaseSnapshotReference() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Product aggregate = (await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))!;
    Guid initialSnapshotId = product.PendingSnapshots.Single().Id.Value;
    var purchase = new PurchaseDbEntity {
      Id = Guid.CreateVersion7(), PurchasedAt = observation.ObservedAt,
      Items = [new PurchaseItemDbEntity { Id = Guid.CreateVersion7(), PriceSnapshotId = initialSnapshotId, Amount = 1 }],
    };
    _context.Purchases.Add(purchase);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    aggregate.RegisterObservation(observation with {
      ObservedAt = observation.ObservedAt.AddDays(1),
      Formats = [new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/new.png"))],
    });

    await _productRepository.SaveBatchAsync([aggregate], TestContext.Current.CancellationToken);
    Guid actual = await _context.PurchaseItems.Select(value => value.PriceSnapshotId).SingleAsync(TestContext.Current.CancellationToken);

    Assert.Equal(initialSnapshotId, actual);
  }

  [Fact]
  public async Task SaveBatchAsync_PreservesShoppingFormatReference() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Product aggregate = (await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))!;
    Guid formatId = product.Formats.Single().Id.Value;
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = Guid.CreateVersion7(), Name = "Weekly",
      Items = [new ShoppingItemDbEntity { Id = Guid.CreateVersion7(), ProductFormatId = formatId, Amount = 1 }],
    });
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    aggregate.RegisterObservation(observation with {
      ObservedAt = observation.ObservedAt.AddDays(1),
      Formats = [new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/new.png"))],
    });

    await _productRepository.SaveBatchAsync([aggregate], TestContext.Current.CancellationToken);
    Guid actual = await _context.ShoppingItems.Select(value => value.ProductFormatId).SingleAsync(TestContext.Current.CancellationToken);

    Assert.Equal(formatId, actual);
  }

  [Fact]
  public async Task GetBatchAsync_LoadsMatchingProduct() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    var identity = new ProductIdentity(product.MarketId, product.BrandId, product.Name);

    IReadOnlyDictionary<ProductIdentity, Product> actual = await _productRepository.GetBatchAsync([identity], TestContext.Current.CancellationToken);

    Assert.Equal(product.Id, actual[identity].Id);
  }

  [Fact]
  public async Task GetBatchAsync_ReturnsNoUnrelatedProducts() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    var identity = new ProductIdentity(product.MarketId, product.BrandId, product.Name);
    var other = Product.Create(observation with { ProductName = "Bread" });
    await _productRepository.SaveBatchAsync([other], TestContext.Current.CancellationToken);

    IReadOnlyDictionary<ProductIdentity, Product> actual = await _productRepository.GetBatchAsync([identity], TestContext.Current.CancellationToken);

    Assert.Single(actual);
  }

  [Fact]
  public async Task GetBatchAsync_DoesNotMatchCrossedIdentityComponents() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    var otherMarket = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Other market" };
    var otherBrand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Other brand" };
    _context.AddRange(otherMarket, otherBrand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var other = Product.Create(observation with { MarketId = otherMarket.Id, BrandId = otherBrand.Id });
    await _productRepository.SaveBatchAsync([other], TestContext.Current.CancellationToken);
    var firstKey = new ProductIdentity(product.MarketId, other.BrandId, product.Name);
    var secondKey = new ProductIdentity(other.MarketId, product.BrandId, product.Name);

    IReadOnlyDictionary<ProductIdentity, Product> actual = await _productRepository.GetBatchAsync([firstKey, secondKey], TestContext.Current.CancellationToken);

    Assert.Empty(actual);
  }

  [Theory]
  [InlineData("Milk", "MILK")]
  [InlineData("Caf\u00e9", "CAF\u00c9")]
  [InlineData("\u00c1gua", "\u00e1gua")]
  public async Task GetBatchAsync_UsesDomainNameIdentity(string storedName, string requestedName) {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation with { ProductName = storedName });
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    var identity = new ProductIdentity(product.MarketId, product.BrandId, ProductName.Create(requestedName));

    IReadOnlyDictionary<ProductIdentity, Product> actual = await _productRepository.GetBatchAsync([identity], TestContext.Current.CancellationToken);

    Assert.Equal(product.Id, actual[identity].Id);
  }

  [Fact]
  public async Task GetBatchAsync_PreservesDisplayName() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    var identity = new ProductIdentity(product.MarketId, product.BrandId, ProductName.Create("MILK"));

    IReadOnlyDictionary<ProductIdentity, Product> actual = await _productRepository.GetBatchAsync([identity], TestContext.Current.CancellationToken);

    Assert.Equal("Milk", actual[identity].Name.Value);
  }

  [Fact]
  public async Task GetBatchAsync_IgnoresDuplicateIdentityRequests() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    var identity = new ProductIdentity(product.MarketId, product.BrandId, product.Name);
    ProductIdentity equivalent = identity with { Name = ProductName.Create("MILK") };

    IReadOnlyDictionary<ProductIdentity, Product> actual = await _productRepository.GetBatchAsync([identity, equivalent], TestContext.Current.CancellationToken);

    Assert.Single(actual);
  }

  [Fact]
  public async Task GetBatchAsync_HydratesAllFormats() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    product.RegisterObservation(observation with {
      Formats = [new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png"))],
    });
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    var identity = new ProductIdentity(product.MarketId, product.BrandId, product.Name);

    IReadOnlyDictionary<ProductIdentity, Product> actual = await _productRepository.GetBatchAsync([identity], TestContext.Current.CancellationToken);

    Assert.Equal(
      product.Formats.Select(value => value.Id.Value).Order(),
      actual[identity].Formats.Select(value => value.Id.Value).Order());
  }

  [Fact]
  public async Task GetBatchAsync_HydratesLatestSnapshot() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    for (int day = 1; day <= 30; day++) {
      product.RegisterObservation(observation with {
        ObservedAt = observation.ObservedAt.AddDays(day),
        Formats = [new ProductFormatObservationParams(1, "l", 1.99m + day, new Uri("https://example.com/milk.png"))],
      });
    }
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    var identity = new ProductIdentity(product.MarketId, product.BrandId, product.Name);

    IReadOnlyDictionary<ProductIdentity, Product> actual = await _productRepository.GetBatchAsync([identity], TestContext.Current.CancellationToken);

    Assert.Equal(product.Formats.Single().LatestSnapshot.Id, actual[identity].Formats.Single().LatestSnapshot.Id);
  }

  [Fact]
  public async Task GetBatchAsync_HydratesWithoutPendingInserts() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    var identity = new ProductIdentity(product.MarketId, product.BrandId, product.Name);

    IReadOnlyDictionary<ProductIdentity, Product> actual = await _productRepository.GetBatchAsync([identity], TestContext.Current.CancellationToken);

    Assert.Empty(actual[identity].PendingSnapshots);
  }

  [Fact]
  public async Task GetByIdAsync_HydratesLatestSnapshot() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    product.RegisterObservation(observation with {
      ObservedAt = observation.ObservedAt.AddDays(1),
      Formats = [new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/milk.png"))],
    });
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);

    Product? actual = await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken);

    Assert.Equal(product.Formats.Single().LatestSnapshot.Id, actual!.Formats.Single().LatestSnapshot.Id);
  }

  [Fact]
  public async Task GetBatchAsync_ReturnsEmptyForMissingIdentity() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    var identity = new ProductIdentity(product.MarketId, product.BrandId, product.Name);

    IReadOnlyDictionary<ProductIdentity, Product> actual = await _productRepository.GetBatchAsync([identity], TestContext.Current.CancellationToken);

    Assert.Empty(actual);
  }

  [Fact]
  public async Task GetBatchAsync_DoesNotQueryForEmptyBatch() {
    var commands = new CommandCounter();
    await using MainContext observed = _fixture.CreateContext(commands);
    var repository = new PostgreSqlMarketProductRepository(observed);

    _ = await repository.GetBatchAsync([], TestContext.Current.CancellationToken);

    Assert.Equal(0, commands.Count);
  }

  [Fact]
  public async Task SaveBatchAsync_DoesNotQueryForEmptyBatch() {
    var commands = new CommandCounter();
    await using MainContext observed = _fixture.CreateContext(commands);
    var repository = new PostgreSqlMarketProductRepository(observed);

    await repository.SaveBatchAsync([], TestContext.Current.CancellationToken);

    Assert.Equal(0, commands.Count);
  }

  [Fact]
  public async Task SaveBatchAsync_PersistsAllRequestedProducts() {
    const int productCount = 1_001;
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    Product[] products = [.. Enumerable.Range(0, productCount)
      .Select(index => Product.Create(observation with { ProductName = $"Product {index}" }))];

    await _productRepository.SaveBatchAsync(products, TestContext.Current.CancellationToken);
    int count = await _context.PriceSnapshots.CountAsync(TestContext.Current.CancellationToken);

    Assert.Equal(productCount, count);
  }

  [Fact]
  public async Task GetBatchAsync_LoadsAllRequestedProducts() {
    const int productCount = 1_001;
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    Product[] products = [.. Enumerable.Range(0, productCount)
      .Select(index => Product.Create(observation with { ProductName = $"Product {index}" }))];
    await _productRepository.SaveBatchAsync(products, TestContext.Current.CancellationToken);
    ProductIdentity[] keys = [.. products.Select(value => new ProductIdentity(value.MarketId, value.BrandId, value.Name))];
    var commands = new CommandCounter();
    await using MainContext observed = _fixture.CreateContext(commands);
    var repository = new PostgreSqlMarketProductRepository(observed);

    IReadOnlyDictionary<ProductIdentity, Product> actual = await repository.GetBatchAsync(keys, TestContext.Current.CancellationToken);

    Assert.Equal(productCount, actual.Count);
  }

  [Fact]
  public async Task Migration_RejectsCaseInsensitiveProductCollisions() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    IMigrator migrator = _context.GetService<IMigrator>();
    var firstId = Guid.CreateVersion7();
    var secondId = Guid.CreateVersion7();
    Exception? failure;
    try {
      await migrator.MigrateAsync("20261025134834_AddCatalogAdminRole", TestContext.Current.CancellationToken);
      _context.Products.AddRange(
        new ProductDbEntity { Id = firstId, Name = "Milk", SuperMarketId = market.Id, BrandId = brand.Id },
        new ProductDbEntity { Id = secondId, Name = "MILK", SuperMarketId = market.Id, BrandId = brand.Id });
      await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
      failure = await Record.ExceptionAsync(() => migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken));
    } finally {
      await _context.Products.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
      await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
    }

    Assert.IsType<PostgresException>(failure);
  }

  [Fact]
  public async Task Migration_PreservesConflictingProductIdentifiers() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    IMigrator migrator = _context.GetService<IMigrator>();
    var firstId = Guid.CreateVersion7();
    var secondId = Guid.CreateVersion7();
    Guid[] remaining;
    try {
      await migrator.MigrateAsync("20261025134834_AddCatalogAdminRole", TestContext.Current.CancellationToken);
      _context.Products.AddRange(
        new ProductDbEntity { Id = firstId, Name = "Milk", SuperMarketId = market.Id, BrandId = brand.Id },
        new ProductDbEntity { Id = secondId, Name = "MILK", SuperMarketId = market.Id, BrandId = brand.Id });
      await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
      _ = await Record.ExceptionAsync(() => migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken));
      remaining = await _context.Products.OrderBy(value => value.Id).Select(value => value.Id)
        .ToArrayAsync(TestContext.Current.CancellationToken);
    } finally {
      await _context.Products.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
      await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
    }

    Assert.Equal(new[] { firstId, secondId }.Order(), remaining);
  }

  [Fact]
  public async Task GetBatchAsync_LoadsManyProductsWithoutPerProductQueries() {
    const int productCount = 1_001;
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    Product[] products = [.. Enumerable.Range(0, productCount)
      .Select(index => Product.Create(observation with { ProductName = $"Product {index}" }))];
    await _productRepository.SaveBatchAsync(products, TestContext.Current.CancellationToken);
    ProductIdentity[] keys = [.. products.Select(value => new ProductIdentity(value.MarketId, value.BrandId, value.Name))];
    var commands = new CommandCounter();
    await using MainContext observed = _fixture.CreateContext(commands);
    var repository = new PostgreSqlMarketProductRepository(observed);

    _ = await repository.GetBatchAsync(keys, TestContext.Current.CancellationToken);

    Assert.InRange(commands.Count, 1, 3);
  }

  [Fact]
  public async Task ResolveProductsAsync_ReusesProductUnderNewIdentityCollation() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    var import = new MarketImport(new MarketName("Market"), [new ProductImport(
      ProductName.Create("MILK"), new BrandName("Brand"),
      [new ProductFormatImport(product.Formats.Single().Quantity, product.Formats.Single().LatestSnapshot.Price, product.Formats.Single().ImageUrl)])]);

    ProductImportResult actual = await _productRepository.ResolveProductsAsync(import, observation.ObservedAt.AddDays(1), TestContext.Current.CancellationToken);

    Assert.Empty(actual.AddedProductIds);
  }

  private sealed class CommandCounter : DbCommandInterceptor {
    internal int Count { get; private set; }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
      DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
      CancellationToken cancellationToken = default
    ) {
      Count++;
      return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
      DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
      CancellationToken cancellationToken = default
    ) {
      Count++;
      return ValueTask.FromResult(result);
    }
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