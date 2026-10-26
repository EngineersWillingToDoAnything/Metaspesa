using Metaspesa.Database.Repositories;
using Metaspesa.Domain.Markets;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Metaspesa.Database.IntegrationTests.Markets;

[Collection("Database")]
public sealed class PostgreSqlBrandRepositoryTest : IAsyncLifetime {
  private readonly DatabaseFixture _fixture;
  private readonly MainContext _context;
  private readonly PostgreSqlBrandRepository _repository;

  public PostgreSqlBrandRepositoryTest(DatabaseFixture fixture) {
    _fixture = fixture;
    _context = fixture.CreateContext();
    _repository = new PostgreSqlBrandRepository(_context);
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
  }

  [Theory]
  [InlineData("Campofrío", " CAMPOFRÍO ")]
  [InlineData("A_%", "a_%")]
  [InlineData("Straße", "STRAßE")]
  [InlineData("Σ", "ς")]
  [InlineData("Café", "CAFE\u0301")]
  public async Task ResolveBrandsAsync_ResolvesLiteralNamesIgnoringCase(
    string displayName, string requestedName
  ) {
    // Arrange
    var aggregate = Brand.Create(displayName);
    await _repository.SaveAsync(aggregate, TestContext.Current.CancellationToken);

    // Act
    IReadOnlyDictionary<string, Guid> result = await _repository.ResolveBrandsAsync(
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
  public async Task ResolveBrandsAsync_DoesNotMatchDifferentNames(
    string existingName, string requestedName
  ) {
    // Arrange
    var brand = Brand.Create(existingName);
    await _repository.SaveAsync(brand, TestContext.Current.CancellationToken);

    // Act
    IReadOnlyDictionary<string, Guid> result = await _repository.ResolveBrandsAsync(
      [requestedName], TestContext.Current.CancellationToken);

    // Assert
    Assert.Empty(result);
  }

  [Fact]
  public async Task ResolveBrandsAsync_ReturnsEmptyForNoRequestedNames() {
    // Act
    IReadOnlyDictionary<string, Guid> result = await _repository.ResolveBrandsAsync(
      [], TestContext.Current.CancellationToken);

    // Assert
    Assert.Empty(result);
  }

  [Fact]
  public async Task ExistsAsync_ReturnsTrueForExistingNameIgnoringCase() {
    // Arrange
    var brand = Brand.Create("Name");
    await _repository.SaveAsync(brand, TestContext.Current.CancellationToken);

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
    var brand = Brand.Create("Name");
    await _repository.SaveAsync(brand, TestContext.Current.CancellationToken);

    // Act
    bool exists = await _repository.ExistsAsync(
      requestedName, TestContext.Current.CancellationToken);

    // Assert
    Assert.False(exists);
  }

  [Fact]
  public async Task SaveAsync_PersistsIdentity() {
    // Arrange
    var aggregate = Brand.Create("Name");

    // Act
    await _repository.SaveAsync(aggregate, TestContext.Current.CancellationToken);

    // Assert
    Guid actual = await _context.ProductBrands.Select(value => value.Id)
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal(aggregate.Id.Value, actual);
  }

  [Fact]
  public async Task SaveAsync_PersistsName() {
    // Arrange
    var aggregate = Brand.Create("Name");

    // Act
    await _repository.SaveAsync(aggregate, TestContext.Current.CancellationToken);

    // Assert
    string actual = await _context.ProductBrands.Select(value => value.Name)
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal("Name", actual);
  }

  [Fact]
  public async Task SaveAsync_UpdatesExistingAggregateName() {
    // Arrange
    var aggregate = Brand.Create("Original");
    await _repository.SaveAsync(aggregate, TestContext.Current.CancellationToken);
    var updated = Brand.Rehydrate(aggregate.Id, new BrandName("Updated"));

    // Act
    await _repository.SaveAsync(updated, TestContext.Current.CancellationToken);

    // Assert
    string actual = await _context.ProductBrands.Select(value => value.Name)
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal(updated.Name.Value, actual);
  }

  [Fact]
  public async Task SaveAsync_RejectsDuplicateName() {
    // Arrange
    var original = Brand.Create("Name");
    await _repository.SaveAsync(original, TestContext.Current.CancellationToken);
    var duplicate = Brand.Create("NAME");

    // Act
    Exception? failure = await Record.ExceptionAsync(() =>
      _repository.SaveAsync(duplicate, TestContext.Current.CancellationToken));

    // Assert
    Assert.IsType<PostgresException>(failure);
  }
}