using System.Data.Common;
using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Application.Markets;
using Metaspesa.Database.Entities;
using Metaspesa.Database.Repositories;
using Metaspesa.Domain.Markets;
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

  public PostgreSqlMarketProductRepositoryTests(DatabaseFixture fixture) {
    _fixture = fixture;
    _context = fixture.CreateContext();
    _productRepository = new PostgreSqlMarketProductRepository(_context);
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

  [Fact]
  public async Task Repository_ExcludesProductWithoutSnapshot_FromCatalog() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Mercadona" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    _context.Products.Add(new ProductDbEntity { Id = Guid.CreateVersion7(), Name = "Milk", BrandId = brand.Id, SuperMarketId = market.Id });
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

    PagedResult<CatalogProduct> result = await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(null, [], null, new Pagination(1, 24)), TestContext.Current.CancellationToken);

    Assert.Empty(result.Values);
  }

  [Fact]
  public async Task Repository_ProjectsLatestSnapshot_InCatalog() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Mercadona" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(market.Id, brand.Id, "Milk",
      new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);

    Product loaded = (await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))!;
    loaded.RegisterObservation(observation with {
      ObservedOn = observation.ObservedOn.AddDays(1),
      Formats = [new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/milk.png"))],
    });
    await _productRepository.SaveBatchAsync([loaded], TestContext.Current.CancellationToken);

    PagedResult<CatalogProduct> result = await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(null, [], null, new Pagination(1, 24)), TestContext.Current.CancellationToken);

    Assert.Equal(2.49m, result.Values.Single().Formats.Single().Price);
  }

  [Fact]
  public async Task Repository_ProjectsProductFormatIdentifier_InCatalog() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Mercadona" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(market.Id, brand.Id, "Milk",
      new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);

    PagedResult<CatalogProduct> result = await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(null, [], null, new Pagination(1, 24)), TestContext.Current.CancellationToken);

    Assert.Equal(product.Formats.Single().Id.Value, result.Values.Single().Formats.Single().Id);
  }

  [Fact]
  public async Task Repository_ProjectsProductIdentity_InCatalog() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Mercadona" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(market.Id, brand.Id, "Milk",
      new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);

    PagedResult<CatalogProduct> result = await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(null, [], null, new Pagination(1, 24)), TestContext.Current.CancellationToken);

    CatalogProduct actual = result.Values.Single();
    Assert.Equal(("Milk", "Brand", market.Id), (actual.Name, actual.Brand, actual.Market.Id));
  }

  [Fact]
  public async Task Repository_FiltersCatalogByName() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Mercadona" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(market.Id, brand.Id, "Milk",
      new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);

    var other = Product.Create(observation with { ProductName = "Yogurt" });
    await _productRepository.SaveBatchAsync([other], TestContext.Current.CancellationToken);

    PagedResult<CatalogProduct> result = await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter("MIL", [], null, new Pagination(1, 24)), TestContext.Current.CancellationToken);

    Assert.Equal(product.Id.Value, result.Values.Single().Id);
  }

  [Fact]
  public async Task Repository_FiltersCatalogByBrand() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Mercadona" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(market.Id, brand.Id, "Milk",
      new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);

    var otherBrand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Other" };
    _context.Add(otherBrand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var other = Product.Create(observation with { BrandId = otherBrand.Id });
    await _productRepository.SaveBatchAsync([other], TestContext.Current.CancellationToken);

    PagedResult<CatalogProduct> result = await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(null, [], "RAN", new Pagination(1, 24)), TestContext.Current.CancellationToken);

    Assert.Equal(product.Id.Value, result.Values.Single().Id);
  }

  [Fact]
  public async Task Repository_FiltersCatalogByMarket() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Mercadona" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(market.Id, brand.Id, "Milk",
      new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);

    var otherMarket = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Other" };
    _context.Add(otherMarket);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var other = Product.Create(observation with { MarketId = otherMarket.Id });
    await _productRepository.SaveBatchAsync([other], TestContext.Current.CancellationToken);

    PagedResult<CatalogProduct> result = await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(null, [new MarketId(market.Id)], null, new Pagination(1, 24)), TestContext.Current.CancellationToken);

    Assert.Equal(product.Id.Value, result.Values.Single().Id);
  }

  [Fact]
  public async Task Repository_ReturnsEmptyPageForMissingMarket() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Mercadona" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(market.Id, brand.Id, "Milk",
      new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);

    PagedResult<CatalogProduct> result = await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(null, [new MarketId(Guid.CreateVersion7())], null, new Pagination(1, 24)), TestContext.Current.CancellationToken);

    Assert.Empty(result.Values);
  }

  [Fact]
  public async Task Repository_PaginatesProducts() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Mercadona" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(market.Id, brand.Id, "Milk",
      new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);

    var other = Product.Create(observation with {
      ProductName = "Yogurt",
      Formats = [new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/yogurt.png"))]
    });
    await _productRepository.SaveBatchAsync([other], TestContext.Current.CancellationToken);

    PagedResult<CatalogProduct> result = await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(null, [], null, new Pagination(2, 1)), TestContext.Current.CancellationToken);

    Assert.Equal(other.Id.Value, result.Values.Single().Id);
  }

  [Fact]
  public async Task Repository_PreservesTotalCountAcrossPages() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Mercadona" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(market.Id, brand.Id, "Milk",
      new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);

    var other = Product.Create(observation with {
      ProductName = "Yogurt",
      Formats = [new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/yogurt.png"))]
    });
    await _productRepository.SaveBatchAsync([other], TestContext.Current.CancellationToken);

    PagedResult<CatalogProduct> result = await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(null, [], null, new Pagination(2, 1)), TestContext.Current.CancellationToken);

    Assert.Equal(2, result.TotalCount);
  }

  [Theory]
  [InlineData(CatalogSort.PriceAsc)]
  [InlineData(CatalogSort.PriceDesc)]
  public async Task Repository_SortsByLatestPrice(CatalogSort sort) {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Mercadona" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(market.Id, brand.Id, "Milk",
      new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);

    var other = Product.Create(observation with {
      ProductName = "Yogurt",
      Formats = [new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/yogurt.png"))]
    });
    await _productRepository.SaveBatchAsync([other], TestContext.Current.CancellationToken);
    Product loaded = (await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))!;
    loaded.RegisterObservation(observation with {
      ObservedOn = observation.ObservedOn.AddDays(1),
      Formats = [new ProductFormatObservationParams(1, "l", 3.49m, new Uri("https://example.com/milk.png"))],
    });
    await _productRepository.SaveBatchAsync([loaded], TestContext.Current.CancellationToken);

    PagedResult<CatalogProduct> result = await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter(null, [], null, new Pagination(1, 1), sort), TestContext.Current.CancellationToken);

    Assert.Equal(sort == CatalogSort.PriceAsc ? other.Id.Value : product.Id.Value, result.Values.Single().Id);
  }

  [Fact]
  public async Task CheckFormatsExistsAsync_ReturnsTrue_WhenAllFormatsExist() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Mercadona" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(market.Id, brand.Id, "Milk",
      new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);

    bool result = await _productRepository.CheckFormatsExistAsync(
      [product.Formats.Single().Id.Value], TestContext.Current.CancellationToken);

    Assert.True(result);
  }

  [Fact]
  public async Task CheckFormatsExistsAsync_ReturnsFalse_WhenAnyFormatIsMissing() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Mercadona" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(market.Id, brand.Id, "Milk",
      new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);

    bool result = await _productRepository.CheckFormatsExistAsync(
      [product.Formats.Single().Id.Value, Guid.CreateVersion7()], TestContext.Current.CancellationToken);

    Assert.False(result);
  }

  [Fact]
  public async Task CheckFormatsExistsAsync_ReturnsTrue_WhenFormatIdsRepeat() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Mercadona" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(market.Id, brand.Id, "Milk",
      new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);

    bool result = await _productRepository.CheckFormatsExistAsync(
      [product.Formats.Single().Id.Value, product.Formats.Single().Id.Value], TestContext.Current.CancellationToken);

    Assert.True(result);
  }

  [Fact]
  public async Task CheckFormatsExistsAsync_ReturnsTrue_WhenNoFormatsRequested() {


    bool result = await _productRepository.CheckFormatsExistAsync([], TestContext.Current.CancellationToken);

    Assert.True(result);
  }

  [Theory]
  [InlineData("kg")]
  [InlineData("ML")]
  [InlineData("box")]
  [InlineData("")]
  public async Task Database_RejectsNonCanonicalUnitCode(string code) {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Mercadona" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(market.Id, brand.Id, "Milk",
      new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);

    Task action() => _context.Database.ExecuteSqlInterpolatedAsync(
      $"UPDATE market.product_formats SET unit_of_measure = {code}", TestContext.Current.CancellationToken);

    PostgresException error = await Assert.ThrowsAsync<PostgresException>(action);
    Assert.Equal("chk_product_format_unit_of_measure", error.ConstraintName);
  }


  [Fact]
  public async Task SaveBatchAsync_PreservesProductId() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);

    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    decimal actual = await _context.PriceSnapshots.Select(value => value.PriceAmount).SingleAsync(TestContext.Current.CancellationToken);

    Assert.Equal(product.PendingSnapshots.Single().Price.Amount, actual);
  }

  [Fact]
  public async Task SaveBatchAsync_PersistsSnapshotDate() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);

    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    DateOnly actual = await _context.PriceSnapshots.Select(value => value.ObservedOn).SingleAsync(TestContext.Current.CancellationToken);

    Assert.Equal(new DateOnly(2026, 7, 28), actual);
  }

  [Fact]
  public async Task SaveBatchAsync_DoesNotFlushUnrelatedTrackedChanges() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Product aggregate = (await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))!;
    Guid initialId = product.PendingSnapshots.Single().Id.Value;
    aggregate.RegisterObservation(observation with {
      ObservedOn = observation.ObservedOn.AddDays(1),
      Formats = [new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/new.png"))],
    });

    await _productRepository.SaveBatchAsync([aggregate], TestContext.Current.CancellationToken);
    Guid[] ids = await _context.PriceSnapshots.OrderBy(value => value.ObservedOn).Select(value => value.Id).ToArrayAsync(TestContext.Current.CancellationToken);

    Assert.Equal([initialId, aggregate.PendingSnapshots.Single().Id.Value], ids);
  }

  [Fact]
  public async Task SaveBatchAsync_DoesNotRewriteExistingProductName() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Product aggregate = (await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))!;
    aggregate.RegisterObservation(observation with {
      ObservedOn = observation.ObservedOn.AddDays(1),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Product aggregate = (await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))!;
    aggregate.RegisterObservation(observation with {
      ObservedOn = observation.ObservedOn.AddDays(1),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Product aggregate = (await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))!;
    aggregate.RegisterObservation(observation with {
      ObservedOn = observation.ObservedOn.AddDays(1),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Product aggregate = (await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))!;
    aggregate.RegisterObservation(observation with { ObservedOn = observation.ObservedOn.AddDays(1) });
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Product aggregate = (await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))!;
    Guid initialSnapshotId = product.PendingSnapshots.Single().Id.Value;
    var purchase = new PurchaseDbEntity {
      Id = Guid.CreateVersion7(), PurchasedAt = observation.ObservedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
      Items = [new PurchaseItemDbEntity { Id = Guid.CreateVersion7(), PriceSnapshotId = initialSnapshotId, Amount = 1 }],
    };
    _context.Purchases.Add(purchase);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    aggregate.RegisterObservation(observation with {
      ObservedOn = observation.ObservedOn.AddDays(1),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      ObservedOn = observation.ObservedOn.AddDays(1),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    for (int day = 1; day <= 30; day++) {
      product.RegisterObservation(observation with {
        ObservedOn = observation.ObservedOn.AddDays(day),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    product.RegisterObservation(observation with {
      ObservedOn = observation.ObservedOn.AddDays(1),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
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
  public async Task SaveBatchAsync_PreservesPreviouslyCommittedProducts_WhenLaterSaveFails() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    var valid = Product.Create(observation with { ProductName = "Yogurt" });
    var invalid = Product.Create(observation with {
      ProductName = "Invalid price",
      Formats = [new ProductFormatObservationParams(1, "l", 0, new Uri("https://example.com/invalid.png"))],
    });

    _ = await Record.ExceptionAsync(() => _productRepository
      .SaveBatchAsync([valid, invalid], TestContext.Current.CancellationToken));

    Guid[] actual = await _context.Products.Select(value => value.Id)
      .ToArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal([product.Id.Value], actual);
  }

  [Fact]
  public async Task SaveBatchAsync_PreservesPreviouslyCommittedFormats_WhenLaterSaveFails() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    var valid = Product.Create(observation with { ProductName = "Yogurt" });
    var invalid = Product.Create(observation with {
      ProductName = "Invalid price",
      Formats = [new ProductFormatObservationParams(1, "l", 0, new Uri("https://example.com/invalid.png"))],
    });

    _ = await Record.ExceptionAsync(() => _productRepository
      .SaveBatchAsync([valid, invalid], TestContext.Current.CancellationToken));

    Guid[] actual = await _context.ProductFormats.Select(value => value.Id)
      .ToArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal([product.Formats.Single().Id.Value], actual);
  }

  [Fact]
  public async Task SaveBatchAsync_PreservesPreviouslyCommittedSnapshots_WhenLaterSaveFails() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    var valid = Product.Create(observation with { ProductName = "Yogurt" });
    var invalid = Product.Create(observation with {
      ProductName = "Invalid price",
      Formats = [new ProductFormatObservationParams(1, "l", 0, new Uri("https://example.com/invalid.png"))],
    });

    _ = await Record.ExceptionAsync(() => _productRepository
      .SaveBatchAsync([valid, invalid], TestContext.Current.CancellationToken));

    Guid[] actual = await _context.PriceSnapshots.Select(value => value.Id)
      .ToArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal([product.PendingSnapshots.Single().Id.Value], actual);
  }

  [Fact]
  public async Task SaveBatchAsync_RollsBackAppendedSnapshot_WhenAnotherProductFails() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var product = Product.Create(observation);
    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    Product loaded = (await _productRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))!;
    loaded.RegisterObservation(observation with {
      ObservedOn = observation.ObservedOn.AddDays(1),
      Formats = [new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/milk.png"))],
    });
    var invalid = Product.Create(observation with {
      ProductName = "Invalid price",
      Formats = [new ProductFormatObservationParams(1, "l", 0, new Uri("https://example.com/invalid.png"))],
    });

    _ = await Record.ExceptionAsync(() => _productRepository
      .SaveBatchAsync([loaded, invalid], TestContext.Current.CancellationToken));

    Guid[] actual = await _context.PriceSnapshots.Select(value => value.Id)
      .ToArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal([product.PendingSnapshots.Single().Id.Value], actual);
  }

  [Fact]
  public async Task SaveBatchAsync_PreservesSnapshotHistory_WhenReloadedBatchesHaveUnchangedObservations() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    const int productCount = 1_001;
    Product[] products = [.. Enumerable.Range(0, productCount)
      .Select(index => Product.Create(observation with { ProductName = $"Product {index}" }))];
    foreach (Product[] batch in products.Chunk(1000)) {
      await _productRepository.SaveBatchAsync(batch, TestContext.Current.CancellationToken);
    }
    Guid[] expected = [.. products.Select(product => product.PendingSnapshots.Single().Id.Value).Order()];

    foreach (DateOnly observedOn in new[] { observation.ObservedOn, observation.ObservedOn.AddDays(1) }) {
      foreach (Product[] batch in products.Chunk(1000)) {
        ProductIdentity[] identities = [.. batch.Select(product =>
          new ProductIdentity(product.MarketId, product.BrandId, product.Name))];
        IReadOnlyDictionary<ProductIdentity, Product> loaded =
          await _productRepository.GetBatchAsync(identities, TestContext.Current.CancellationToken);
        foreach (Product product in loaded.Values) {
          product.RegisterObservation(observation with { ProductName = product.Name.Value, ObservedOn = observedOn });
        }
        await _productRepository.SaveBatchAsync([.. loaded.Values], TestContext.Current.CancellationToken);
      }
    }
    Guid[] actual = await _context.PriceSnapshots.OrderBy(snapshot => snapshot.Id)
      .Select(snapshot => snapshot.Id).ToArrayAsync(TestContext.Current.CancellationToken);

    Assert.Equal(expected, actual);
  }

  [Fact]
  public async Task GetProductsAsync_ProjectsLatestPriceAfterMultipleBatchAppends() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    Product[] products = [.. Enumerable.Range(0, 1_001)
      .Select(index => Product.Create(observation with { ProductName = $"Product {index}" }))];
    foreach (Product product in products) {
      product.RegisterObservation(observation with {
        ProductName = product.Name.Value,
        ObservedOn = observation.ObservedOn.AddDays(1),
        Formats = [new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/milk.png"))],
      });
    }
    foreach (Product[] batch in products.Chunk(1000)) {
      await _productRepository.SaveBatchAsync(batch, TestContext.Current.CancellationToken);
    }
    foreach (Product[] batch in products.Chunk(1000)) {
      ProductIdentity[] identities = [.. batch.Select(product =>
        new ProductIdentity(product.MarketId, product.BrandId, product.Name))];
      IReadOnlyDictionary<ProductIdentity, Product> loaded =
        await _productRepository.GetBatchAsync(identities, TestContext.Current.CancellationToken);
      foreach (Product product in loaded.Values) {
        product.RegisterObservation(observation with {
          ProductName = product.Name.Value,
          ObservedOn = observation.ObservedOn.AddDays(2),
          Formats = [new ProductFormatObservationParams(1, "l", 2.99m, new Uri("https://example.com/milk.png"))],
        });
      }
      await _productRepository.SaveBatchAsync([.. loaded.Values], TestContext.Current.CancellationToken);
    }

    PagedResult<CatalogProduct> actual = await _productRepository.GetProductsAsync(
      new GetMarketProductsFilter("Product 1000", [new MarketId(market.Id)], null, new Pagination(1, 24)),
      TestContext.Current.CancellationToken);

    Assert.Equal(2.99m, actual.Values.Single().Formats.Single().Price);
  }

  [Fact]
  public async Task SaveBatchAsync_RetriesWithFreshAggregatesWithoutDuplicatingCommittedSnapshots() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28),
      [new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png"))]);
    var committed = Product.Create(observation);
    await _productRepository.SaveBatchAsync([committed], TestContext.Current.CancellationToken);
    var invalid = Product.Create(observation with {
      ProductName = "Yogurt",
      Formats = [new ProductFormatObservationParams(1, "l", 0, new Uri("https://example.com/yogurt.png"))],
    });
    await Assert.ThrowsAsync<PostgresException>(() =>
      _productRepository.SaveBatchAsync([invalid], TestContext.Current.CancellationToken));
    Product reloaded = (await _productRepository.GetByIdAsync(committed.Id, TestContext.Current.CancellationToken))!;
    reloaded.RegisterObservation(observation);
    var corrected = Product.Create(observation with { ProductName = "Yogurt" });
    Guid[] expected = [.. new[] {
      committed.PendingSnapshots.Single().Id.Value, corrected.PendingSnapshots.Single().Id.Value,
    }.Order()];

    await _productRepository.SaveBatchAsync([reloaded, corrected], TestContext.Current.CancellationToken);
    Guid[] actual = await _context.PriceSnapshots.OrderBy(snapshot => snapshot.Id)
      .Select(snapshot => snapshot.Id).ToArrayAsync(TestContext.Current.CancellationToken);

    Assert.Equal(expected, actual);
  }

  [Fact]
  public async Task SaveBatchAsync_PersistsEveryPendingSnapshotWithItsOwningFormat() {
    var market = new SuperMarketDbEntity { Id = Guid.CreateVersion7(), Name = "Market" };
    var brand = new ProductBrandDbEntity { Id = Guid.CreateVersion7(), Name = "Brand" };
    _context.AddRange(market, brand);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var observation = new ProductObservationParams(
      market.Id, brand.Id, "Milk", new DateOnly(2026, 7, 28), [
        new ProductFormatObservationParams(1, "l", 1.99m, new Uri("https://example.com/milk.png")),
        new ProductFormatObservationParams(2, "l", 3.49m, new Uri("https://example.com/large.png")),
      ]);
    var product = Product.Create(observation);
    product.RegisterObservation(observation with {
      ObservedOn = observation.ObservedOn.AddDays(1),
      Formats = [
        new ProductFormatObservationParams(1, "l", 2.49m, new Uri("https://example.com/milk.png")),
        new ProductFormatObservationParams(2, "l", 3.99m, new Uri("https://example.com/large.png")),
      ],
    });
    product.RegisterObservation(observation with {
      ObservedOn = observation.ObservedOn.AddDays(2),
      Formats = [
        new ProductFormatObservationParams(1, "l", 2.99m, new Uri("https://example.com/milk.png")),
        new ProductFormatObservationParams(2, "l", 4.49m, new Uri("https://example.com/large.png")),
      ],
    });
    var expected = product.Formats.SelectMany(format => format.PendingSnapshots.Select(snapshot => new {
      Id = snapshot.Id.Value, ProductFormatId = format.Id.Value,
    })).OrderBy(value => value.Id).ToArray();

    await _productRepository.SaveBatchAsync([product], TestContext.Current.CancellationToken);
    var actual = await _context.PriceSnapshots.OrderBy(snapshot => snapshot.Id)
      .Select(snapshot => new { snapshot.Id, snapshot.ProductFormatId })
      .ToArrayAsync(TestContext.Current.CancellationToken);

    Assert.Equal(expected, actual);
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

}