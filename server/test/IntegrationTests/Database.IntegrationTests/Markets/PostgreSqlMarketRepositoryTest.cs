using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Database.Entities;
using Metaspesa.Database.Repositories;
using Metaspesa.Domain.Markets;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Metaspesa.Database.IntegrationTests.Markets;

[Collection("Database")]
public sealed class PostgreSqlMarketRepositoryTest : IAsyncLifetime {
  private readonly DatabaseFixture _fixture;
  private readonly MainContext _context;
  private readonly PostgreSqlMarketRepository _repository;

  public PostgreSqlMarketRepositoryTest(DatabaseFixture fixture) {
    _fixture = fixture;
    _context = fixture.CreateContext();
    _repository = new PostgreSqlMarketRepository(_context);
  }

  public async ValueTask InitializeAsync() {
    CancellationToken cancellationToken = TestContext.Current.CancellationToken;
    await _fixture.DeleteShoppingProductReferencesAsync(cancellationToken);
    await _context.PriceSnapshots.ExecuteDeleteAsync(cancellationToken);
    await _context.ProductFormats.ExecuteDeleteAsync(cancellationToken);
    await _context.Products.ExecuteDeleteAsync(cancellationToken);
    await _context.SuperMarkets.ExecuteDeleteAsync(cancellationToken);
    await _context.SuperMarkets.ExecuteDeleteAsync(cancellationToken);
  }

  public async ValueTask DisposeAsync() {
    await _context.DisposeAsync();
  }

  [Theory]
  [InlineData("Campofrío", " CAMPOFRÍO ")]
  [InlineData("A_%", "a_%")]
  [InlineData("Straße", "STRAßE")]
  [InlineData("Σ", "ς")]
  [InlineData("Café", "CAFE\u0301")]
  public async Task ResolveMarketsAsync_ResolvesLiteralNamesIgnoringCase(
    string displayName, string requestedName
  ) {
    // Arrange
    var aggregate = Market.Create(displayName);
    await _repository.SaveAsync(aggregate, TestContext.Current.CancellationToken);

    // Act
    IReadOnlyDictionary<string, Guid> result = await _repository.ResolveMarketsAsync(
      [requestedName], TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(aggregate.Id.Value, result[requestedName.Trim()]);
  }

  [Theory]
  [InlineData("I", "ı")]
  [InlineData("Cafe", "Café")]
  [InlineData("Name", "Different")]
  [InlineData("Name", "%")]
  [InlineData("Name", "_")]
  public async Task ResolveMarketsAsync_DoesNotMatchDifferentNames(
    string existingName, string requestedName
  ) {
    // Arrange
    var market = Market.Create(existingName);
    await _repository.SaveAsync(market, TestContext.Current.CancellationToken);

    // Act
    IReadOnlyDictionary<string, Guid> result = await _repository.ResolveMarketsAsync(
      [requestedName], TestContext.Current.CancellationToken);

    // Assert
    Assert.Empty(result);
  }

  [Fact]
  public async Task ResolveMarketsAsync_ReturnsEmptyForNoRequestedNames() {
    // Act
    IReadOnlyDictionary<string, Guid> result = await _repository.ResolveMarketsAsync(
      [], TestContext.Current.CancellationToken);

    // Assert
    Assert.Empty(result);
  }

  [Fact]
  public async Task ExistsAsync_ReturnsTrueForExistingNameIgnoringCase() {
    // Arrange
    var market = Market.Create("Name");
    await _repository.SaveAsync(market, TestContext.Current.CancellationToken);

    // Act
    bool exists = await _repository.ExistsAsync(
      "NAME", TestContext.Current.CancellationToken);

    // Assert
    Assert.True(exists);
  }

  [Theory]
  [InlineData("Missing")]
  [InlineData("%")]
  [InlineData("_")]
  public async Task ExistsAsync_ReturnsFalseForNonMatchingName(string requestedName) {
    // Arrange
    var market = Market.Create("Name");
    await _repository.SaveAsync(market, TestContext.Current.CancellationToken);

    // Act
    bool exists = await _repository.ExistsAsync(
      requestedName, TestContext.Current.CancellationToken);

    // Assert
    Assert.False(exists);
  }

  [Fact]
  public async Task SaveAsync_PersistsIdentity() {
    // Arrange
    var aggregate = Market.Create("Name");

    // Act
    await _repository.SaveAsync(aggregate, TestContext.Current.CancellationToken);

    // Assert
    Guid actual = await _context.SuperMarkets.Select(value => value.Id)
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal(aggregate.Id.Value, actual);
  }

  [Fact]
  public async Task SaveAsync_PersistsName() {
    // Arrange
    var aggregate = Market.Create("Name");

    // Act
    await _repository.SaveAsync(aggregate, TestContext.Current.CancellationToken);

    // Assert
    string actual = await _context.SuperMarkets.Select(value => value.Name)
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal("Name", actual);
  }

  [Fact]
  public async Task SaveAsync_UpdatesExistingAggregateName() {
    // Arrange
    var aggregate = Market.Create("Original");
    await _repository.SaveAsync(aggregate, TestContext.Current.CancellationToken);
    var updated = Market.Hydrate(aggregate.Id, new MarketName("Updated"), null);

    // Act
    await _repository.SaveAsync(updated, TestContext.Current.CancellationToken);

    // Assert
    string actual = await _context.SuperMarkets.Select(value => value.Name)
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal(updated.Name.Value, actual);
  }

  [Fact]
  public async Task SaveAsync_RejectsDuplicateName() {
    // Arrange
    var original = Market.Create("Name");
    await _repository.SaveAsync(original, TestContext.Current.CancellationToken);
    var duplicate = Market.Create("NAME");

    // Act
    Exception? failure = await Record.ExceptionAsync(() =>
      _repository.SaveAsync(duplicate, TestContext.Current.CancellationToken));

    // Assert
    Assert.IsType<PostgresException>(failure);
  }

  [Fact(DisplayName = "Maps market logo in summaries")]
  public async Task Repository_MapsLogo_InMarketSummary() {
    // Arrange
    _context.SuperMarkets.Add(new SuperMarketDbEntity {
      Id = Guid.CreateVersion7(),
      Name = "Mercadona",
      LogoUrl = "https://example.test/mercadona.png",
    });
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

    // Act
    IReadOnlyCollection<MarketSummary> summaries =
      await _repository.GetMarketSummariesAsync(
        TestContext.Current.CancellationToken);

    MarketSummary summary = summaries.Single();

    // Assert
    Assert.Equal(new Uri("https://example.test/mercadona.png"), summary.LogoUrl);
  }

  [Fact]
  public async Task SaveAsync_PersistsLogo() {
    // Arrange
    var logo = new Uri("https://example.test/logo.png");
    var aggregate = Market.Create("Name", logo);

    // Act
    await _repository.SaveAsync(aggregate, TestContext.Current.CancellationToken);

    // Assert
    string? actual = await _context.SuperMarkets.Select(value => value.LogoUrl)
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal(logo.ToString(), actual);
  }

  [Fact]
  public async Task SaveAsync_UpdatesLogo() {
    // Arrange
    var aggregate = Market.Create("Name");
    await _repository.SaveAsync(aggregate, TestContext.Current.CancellationToken);
    var logo = new ImageUrl(new Uri("https://example.test/updated.png"));
    var updated = Market.Hydrate(aggregate.Id, aggregate.Name, logo);

    // Act
    await _repository.SaveAsync(updated, TestContext.Current.CancellationToken);

    // Assert
    string? actual = await _context.SuperMarkets.Select(value => value.LogoUrl)
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal(logo.Value.ToString(), actual);
  }
}