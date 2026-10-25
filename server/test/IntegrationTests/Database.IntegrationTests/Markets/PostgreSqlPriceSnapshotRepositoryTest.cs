using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Database.Entities;
using Metaspesa.Database.Repositories;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Metaspesa.Database.IntegrationTests.Markets;

[Collection("Database")]
public class PostgreSqlPriceSnapshotRepositoryTest : IAsyncLifetime {
  private readonly DatabaseFixture _fixture;
  private readonly MainContext _context;
  private readonly PostgreSqlMarketProductRepository _productRepository;
  private readonly PostgreSqlPriceSnapshotRepository _snapshotRepository;

  public PostgreSqlPriceSnapshotRepositoryTest(DatabaseFixture fixture) {
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

  [Fact(DisplayName = "Loads latest format snapshot by observation time")]
  public async Task Repository_LoadsLatestSnapshot_ByObservationTime() {
    ProductImportResult result = await ResolveAndAppendAsync();
    ProductFormatId formatId = result.PriceObservations.Single().ProductFormatId;
    await _snapshotRepository.AppendAsync([
      new PriceObservation(formatId, new Money(2.49m), UtcDate(2026, 7, 30)),
      new PriceObservation(formatId, new Money(1.49m), UtcDate(2026, 7, 29)),
    ], TestContext.Current.CancellationToken);

    IReadOnlyCollection<PriceSnapshot> snapshots =
      await _snapshotRepository.GetLatestForFormatsAsync(
        [formatId], TestContext.Current.CancellationToken);

    Assert.Equal(new Money(2.49m), snapshots.Single().Price);
  }

  [Fact(DisplayName = "Uses identifier tie-breaker when observation times match")]
  public async Task Repository_UsesIdTieBreaker_WhenObservationTimesMatch() {
    ProductImportResult result = await ResolveAndAppendAsync();
    ProductFormatId formatId = result.PriceObservations.Single().ProductFormatId;
    await _snapshotRepository.AppendAsync([
      new PriceObservation(formatId, new Money(2.49m), UtcDate(2026, 7, 30)),
      new PriceObservation(formatId, new Money(2.99m), UtcDate(2026, 7, 30)),
    ], TestContext.Current.CancellationToken);
    PriceSnapshotDbEntity expectedSnapshot = await _context.PriceSnapshots
      .Where(snapshot => snapshot.ProductFormatId == formatId.Value)
      .OrderByDescending(snapshot => snapshot.Id)
      .FirstAsync(TestContext.Current.CancellationToken);

    IReadOnlyCollection<PriceSnapshot> snapshots =
      await _snapshotRepository.GetLatestForFormatsAsync(
        [formatId], TestContext.Current.CancellationToken);

    Assert.Equal(new PriceSnapshotId(expectedSnapshot.Id), snapshots.Single().Id);
  }

  [Fact(DisplayName = "Returns no snapshots when requested format has no history")]
  public async Task Repository_ReturnsNoSnapshots_ForUnknownFormat() {
    await ResolveAndAppendAsync();

    IReadOnlyCollection<PriceSnapshot> snapshots =
      await _snapshotRepository.GetLatestForFormatsAsync(
        [new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-00007fffffff"))], TestContext.Current.CancellationToken);

    Assert.Empty(snapshots);
  }

  [Fact(DisplayName = "Appends every observation as a new snapshot")]
  public async Task Repository_AppendsSnapshots_WithoutUpdatingExistingRows() {
    ProductImportResult result = await ResolveAndAppendAsync();
    ProductFormatId formatId = Assert.Single(result.PriceObservations).ProductFormatId;

    await _snapshotRepository.AppendAsync(
      [new PriceObservation(
        formatId,
        new Money(2.49m),
        UtcDate(2026, 7, 29))],
      TestContext.Current.CancellationToken);

    List<decimal> prices = await _context.PriceSnapshots
      .OrderBy(snapshot => snapshot.ObservedAt)
      .Select(snapshot => snapshot.PriceAmount)
      .ToListAsync(TestContext.Current.CancellationToken);
    Assert.Equal([1.99m, 2.49m], prices);
  }

  [Fact(DisplayName = "Deletes only snapshots for matching market and observation")]
  public async Task Repository_DeletesSnapshots_ForMatchingOperationOnly() {
    ProductImportResult result = await ResolveAndAppendAsync();
    ProductFormatId formatId = Assert.Single(result.PriceObservations).ProductFormatId;
    await _snapshotRepository.AppendAsync(
      [new PriceObservation(
        formatId,
        new Money(2.49m),
        UtcDate(2026, 7, 29))],
      TestContext.Current.CancellationToken);

    await _snapshotRepository.DeleteForMarketsAsync(
      [new MarketName("Mercadona")],
      UtcDate(2026, 7, 28),
      TestContext.Current.CancellationToken);

    PriceSnapshotDbEntity remaining = await _context.PriceSnapshots
      .AsNoTracking()
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2.49m, remaining.PriceAmount);
    Assert.Equal(UtcDate(2026, 7, 29), remaining.ObservedAt);
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

  private async Task<MarketImport> CreateMarketImportAsync() {
    _context.SuperMarkets.Add(new SuperMarketDbEntity {
      Id = Uid.Create(),
      Name = "Mercadona",
    });
    _context.ProductBrands.Add(new ProductBrandDbEntity {
      Id = Uid.Create(),
      Name = "Brand",
    });
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

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


  private static DateTime UtcDate(int year, int month, int day) =>
    new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}