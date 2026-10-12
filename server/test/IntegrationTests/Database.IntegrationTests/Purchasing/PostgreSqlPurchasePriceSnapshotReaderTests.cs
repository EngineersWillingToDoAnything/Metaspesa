using Metaspesa.Database.Entities;
using Metaspesa.Database.Repositories;
using Metaspesa.Domain.Markets;
using Microsoft.EntityFrameworkCore;

namespace Metaspesa.Database.IntegrationTests.Purchasing;

[Collection("Database")]
public class PostgreSqlPurchasePriceSnapshotReaderTests : IAsyncLifetime {
  private readonly DatabaseFixture _fixture;
  private readonly MainContext _context;
  private readonly PostgreSqlPurchasePriceSnapshotReader _reader;

  public PostgreSqlPurchasePriceSnapshotReaderTests(DatabaseFixture fixture) {
    _fixture = fixture;
    _context = fixture.CreateContext();
    _reader = new PostgreSqlPurchasePriceSnapshotReader(_context);
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
  public async Task GetSnapshotsIdAsync_ReturnsEmpty_WhenInputIsEmpty() {
    IReadOnlyDictionary<Guid, Guid> result = await _reader.GetSnapshotsIdAsync(
      [], UtcDate(2026, 8, 19), TestContext.Current.CancellationToken);

    Assert.Empty(result);
  }

  [Fact]
  public async Task GetSnapshotsIdAsync_ReturnsLatestEligibleSnapshot() {
    ProductFormatId formatId = await SeedFormatAsync("Milk");
    await SeedSnapshotAsync(formatId, UtcDate(2026, 8, 17), 1m);
    PriceSnapshotId eligible = await SeedSnapshotAsync(
      formatId, UtcDate(2026, 8, 18), 2m);
    await SeedSnapshotAsync(formatId, UtcDate(2026, 8, 20), 3m);

    IReadOnlyDictionary<Guid, Guid> result = await _reader.GetSnapshotsIdAsync(
      [formatId.Value], UtcDate(2026, 8, 19), TestContext.Current.CancellationToken);

    Assert.Equal(eligible.Value, result[formatId.Value]);
  }

  [Fact]
  public async Task GetSnapshotsIdAsync_IncludesSnapshotAtExactCheckoutTime() {
    ProductFormatId formatId = await SeedFormatAsync("Milk");
    DateTime checkoutAt = UtcDate(2026, 8, 19);
    PriceSnapshotId snapshot = await SeedSnapshotAsync(formatId, checkoutAt, 2m);

    IReadOnlyDictionary<Guid, Guid> result = await _reader.GetSnapshotsIdAsync(
      [formatId.Value], checkoutAt, TestContext.Current.CancellationToken);

    Assert.Equal(snapshot.Value, result[formatId.Value]);
  }

  [Fact]
  public async Task GetSnapshotsIdAsync_BreaksEqualTimeTieWithHighestId() {
    ProductFormatId formatId = await SeedFormatAsync("Milk");
    DateTime checkoutAt = UtcDate(2026, 8, 19);
    PriceSnapshotId first = await SeedSnapshotAsync(formatId, checkoutAt, 1m);
    PriceSnapshotId second = await SeedSnapshotAsync(formatId, checkoutAt, 2m);
    Guid expected = first.Value.CompareTo(second.Value) > 0
      ? first.Value : second.Value;

    IReadOnlyDictionary<Guid, Guid> result = await _reader.GetSnapshotsIdAsync(
      [formatId.Value], checkoutAt, TestContext.Current.CancellationToken);

    Assert.Equal(expected, result[formatId.Value]);
  }

  [Fact]
  public async Task GetSnapshotsIdAsync_OmitsFormatWithoutSnapshots() {
    ProductFormatId formatId = await SeedFormatAsync("Milk");

    IReadOnlyDictionary<Guid, Guid> result = await _reader.GetSnapshotsIdAsync(
      [formatId.Value], UtcDate(2026, 8, 19), TestContext.Current.CancellationToken);

    Assert.Empty(result);
  }

  [Fact]
  public async Task GetSnapshotsIdAsync_OmitsFormatWithOnlyFutureSnapshots() {
    ProductFormatId formatId = await SeedFormatAsync("Milk");
    await SeedSnapshotAsync(formatId, UtcDate(2026, 8, 20), 3m);

    IReadOnlyDictionary<Guid, Guid> result = await _reader.GetSnapshotsIdAsync(
      [formatId.Value], UtcDate(2026, 8, 19), TestContext.Current.CancellationToken);

    Assert.Empty(result);
  }

  [Fact]
  public async Task GetSnapshotsIdAsync_ResolvesMultipleFormats() {
    ProductFormatId milk = await SeedFormatAsync("Milk");
    ProductFormatId bread = await SeedFormatAsync("Bread");
    PriceSnapshotId milkSnapshot = await SeedSnapshotAsync(
      milk, UtcDate(2026, 8, 18), 1m);
    PriceSnapshotId breadSnapshot = await SeedSnapshotAsync(
      bread, UtcDate(2026, 8, 18), 2m);

    IReadOnlyDictionary<Guid, Guid> result = await _reader.GetSnapshotsIdAsync(
      [milk.Value, bread.Value], UtcDate(2026, 8, 19),
      TestContext.Current.CancellationToken);

    Assert.Equal(new[] { milkSnapshot.Value, breadSnapshot.Value }.Order(),
      result.Values.Order());
  }

  [Fact]
  public async Task GetSnapshotsIdAsync_DeduplicatesRequestedFormats() {
    ProductFormatId formatId = await SeedFormatAsync("Milk");
    await SeedSnapshotAsync(formatId, UtcDate(2026, 8, 18), 1m);

    IReadOnlyDictionary<Guid, Guid> result = await _reader.GetSnapshotsIdAsync(
      [formatId.Value, formatId.Value], UtcDate(2026, 8, 19),
      TestContext.Current.CancellationToken);

    Assert.Single(result);
  }

  [Fact]
  public async Task GetSnapshotsIdAsync_PropagatesCancellation() {
    ProductFormatId formatId = await SeedFormatAsync("Milk");
    using var source = new CancellationTokenSource();
    await source.CancelAsync();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
      _reader.GetSnapshotsIdAsync([formatId.Value], UtcDate(2026, 8, 19),
        source.Token));
  }

  private async Task<ProductFormatId> SeedFormatAsync(string name) {
    var market = new SuperMarketDbEntity {
      Id = Guid.CreateVersion7(),
      Name = $"Market {Guid.CreateVersion7()}",
    };
    var brand = new ProductBrandDbEntity {
      Id = Guid.CreateVersion7(),
      Name = $"Brand {Guid.CreateVersion7()}",
    };
    var unit = new UnitOfMeasureDbEntity {
      Id = Guid.CreateVersion7(),
      Code = $"u{Guid.NewGuid():N}"[..16],
      Name = $"Unit {Guid.CreateVersion7()}",
    };
    var format = new ProductFormatDbEntity {
      Id = Guid.CreateVersion7(),
      Product = new ProductDbEntity {
        Id = Guid.CreateVersion7(),
        Name = name,
        SuperMarket = market,
        Brand = brand,
      },
      Quantity = 1,
      UnitOfMeasure = unit,
      ImageUrl = string.Empty,
    };
    _context.ProductFormats.Add(format);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    return new ProductFormatId(format.Id);
  }

  private async Task<PriceSnapshotId> SeedSnapshotAsync(
    ProductFormatId formatId,
    DateTime observedAt,
    decimal price
  ) {
    var snapshot = new PriceSnapshotDbEntity {
      Id = Guid.CreateVersion7(),
      ProductFormatId = formatId.Value,
      PriceAmount = price,
      ObservedAt = observedAt,
    };
    _context.PriceSnapshots.Add(snapshot);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    return new PriceSnapshotId(snapshot.Id);
  }

  private static DateTime UtcDate(int year, int month, int day) =>
    new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}