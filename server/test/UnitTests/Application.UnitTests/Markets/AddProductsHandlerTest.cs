using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static Metaspesa.Application.Markets.AddMarketProducts;

namespace Metaspesa.Application.UnitTests.Markets;

public sealed class AddProductsHandlerTest : IDisposable {
  private readonly IProductRepository _productRepository = Substitute.For<IProductRepository>();
  private readonly IMarketRepository _marketRepository = Substitute.For<IMarketRepository>();
  private readonly IBrandRepository _brandRepository = Substitute.For<IBrandRepository>();
  private readonly IPriceSnapshotRepository _snapshotRepository = Substitute.For<IPriceSnapshotRepository>();
  private readonly ServiceProvider _services;

  private readonly Handler _handler;

  public AddProductsHandlerTest() {
    _marketRepository.ResolveMarketsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(KnownMarkets());
    _brandRepository.ResolveBrandsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(KnownBrands());
    _snapshotRepository.GetLatestForFormatsAsync(Arg.Any<IReadOnlyCollection<ProductFormatId>>(), Arg.Any<CancellationToken>()).Returns([]);
    _productRepository.ResolveProductsAsync(Arg.Any<MarketImport>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
      .Returns(new ProductImportResult([], [], []));
    _services = new ServiceCollection().AddSingleton(_productRepository).AddSingleton(_snapshotRepository).BuildServiceProvider();
    _handler = new Handler(_marketRepository, _brandRepository, _productRepository, _snapshotRepository,
      _services.GetRequiredService<IServiceScopeFactory>(), Substitute.For<ILogger<Handler>>());
  }

  [Fact]
  public async Task Handler_RejectsEmptyImport() {
    // Arrange
    var command = new Command([], new DateOnly(2026, 7, 28));

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    Assert.IsType<EmptyMarketProductsException>(exception);
  }

  [Fact]
  public async Task Handler_RejectsOldObservationDate() {
    // Arrange
    Command command = CreateCommand() with { RegisteredAt = new DateOnly(2022, 12, 31) };

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    Assert.IsType<InvalidMarketProductsRegisteredAtException>(exception);
  }

  [Fact]
  public async Task Handler_RejectsInvalidProductName() {
    // Arrange
    Command original = CreateCommand();
    Command command = original with { Products = [original.Products.Single() with { Name = null }] };

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    Assert.IsType<InvalidProductNameException>(exception);
  }

  [Fact]
  public async Task Handler_RejectsDuplicate_RepeatedIdentity() {
    // Arrange
    Command original = CreateCommand();
    CommandProduct product = original.Products.Single();
    Command command = original with { Products = [product, product] };

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    Assert.IsType<DuplicateMarketProductException>(exception);
  }

  [Fact]
  public async Task Handler_RejectsDuplicate_CaseVariantName() {
    // Arrange
    Command original = CreateCommand();
    CommandProduct product = original.Products.Single();
    Command command = original with { Products = [product, product with { Name = "MILK" }] };

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    Assert.IsType<DuplicateMarketProductException>(exception);
  }

  [Fact]
  public async Task Handler_RejectsDuplicate_CaseVariantMarket() {
    // Arrange
    Command original = CreateCommand();
    CommandProduct product = original.Products.Single();
    Command command = original with { Products = [product, product with { MarketName = "MARKET" }] };

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    Assert.IsType<DuplicateMarketProductException>(exception);
  }

  [Fact]
  public async Task Handler_RejectsDuplicate_CaseVariantBrand() {
    // Arrange
    Command original = CreateCommand();
    CommandProduct product = original.Products.Single();
    Command command = original with { Products = [product, product with { BrandName = "BRAND" }] };

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    Assert.IsType<DuplicateMarketProductException>(exception);
  }

  [Fact]
  public async Task Handler_RejectsDuplicate_CaseVariantUnit() {
    // Arrange
    Command original = CreateCommand();
    CommandProduct product = original.Products.Single();
    Command command = original with { Products = [product, product with { UnitOfMeasure = "L" }] };

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    Assert.IsType<DuplicateMarketProductException>(exception);
  }

  [Fact]
  public async Task Handler_RejectsDuplicate_DifferentImage() {
    // Arrange
    Command original = CreateCommand();
    CommandProduct product = original.Products.Single();
    Command command = original with { Products = [product, product with { ImageUrl = new Uri("https://example.test/other.png") }] };

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    Assert.IsType<DuplicateMarketProductException>(exception);
  }

  [Fact]
  public async Task Handler_RejectsDuplicate_DifferentPrice() {
    // Arrange
    Command original = CreateCommand();
    CommandProduct product = original.Products.Single();
    Command command = original with { Products = [product, product with { Price = 2.99m }] };

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    Assert.IsType<DuplicateMarketProductException>(exception);
  }

  [Fact]
  public async Task Handler_RejectsDuplicate_EquivalentCanonicalQuantity() {
    // Arrange
    Command original = CreateCommand();
    CommandProduct product = original.Products.Single();
    Command command = original with { Products = [product, product with { Quantity = 1000m, UnitOfMeasure = "ml" }] };

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    Assert.IsType<DuplicateMarketProductException>(exception);
  }

  [Fact]
  public async Task Handler_Accepts_DifferentProductNames() {
    // Arrange
    Command original = CreateCommand();
    CommandProduct product = original.Products.Single();
    Command command = original with { Products = [product, product with { Name = "Bread" }] };
    var resolved = new List<MarketImport>();
    _productRepository.ResolveProductsAsync(Arg.Do<MarketImport>(market => resolved.Add(market)),
      Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(new ProductImportResult([], [], []));

    // Act
    await _handler.Handle(command, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(2, resolved.Single().Products.Count);
  }

  [Fact]
  public async Task Handler_Accepts_DifferentMarkets() {
    // Arrange
    Command original = CreateCommand();
    CommandProduct product = original.Products.Single();
    Command command = original with { Products = [product, product with { MarketName = "Other market" }] };
    var resolved = new List<MarketImport>();
    _productRepository.ResolveProductsAsync(Arg.Do<MarketImport>(market => resolved.Add(market)),
      Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(new ProductImportResult([], [], []));

    // Act
    await _handler.Handle(command, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(2, resolved.Count);
  }

  [Fact]
  public async Task Handler_Accepts_DifferentBrands() {
    // Arrange
    Command original = CreateCommand();
    CommandProduct product = original.Products.Single();
    Command command = original with { Products = [product, product with { BrandName = "Other brand" }] };
    var resolved = new List<MarketImport>();
    _productRepository.ResolveProductsAsync(Arg.Do<MarketImport>(market => resolved.Add(market)),
      Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(new ProductImportResult([], [], []));

    // Act
    await _handler.Handle(command, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(2, resolved.Single().Products.Count);
  }

  [Fact]
  public async Task Handler_Accepts_DifferentQuantities() {
    // Arrange
    Command original = CreateCommand();
    CommandProduct product = original.Products.Single();
    Command command = original with { Products = [product, product with { Quantity = 2m }] };
    var resolved = new List<MarketImport>();
    _productRepository.ResolveProductsAsync(Arg.Do<MarketImport>(market => resolved.Add(market)),
      Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(new ProductImportResult([], [], []));

    // Act
    await _handler.Handle(command, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(2, resolved.Single().Products.Single().Formats.Count);
  }

  [Fact]
  public async Task Handler_Accepts_DistinctDatabasePrecision() {
    // Arrange
    Command original = CreateCommand();
    CommandProduct product = original.Products.Single();
    Command command = original with { Products = [product, product with { Quantity = 1.001m }] };
    var resolved = new List<MarketImport>();
    _productRepository.ResolveProductsAsync(Arg.Do<MarketImport>(market => resolved.Add(market)),
      Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(new ProductImportResult([], [], []));

    // Act
    await _handler.Handle(command, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(2, resolved.Single().Products.Single().Formats.Count);
  }

  [Fact]
  public async Task Handler_Accepts_DifferentUnits() {
    // Arrange
    Command original = CreateCommand();
    CommandProduct product = original.Products.Single();
    Command command = original with { Products = [product, product with { UnitOfMeasure = "kg" }] };
    var resolved = new List<MarketImport>();
    _productRepository.ResolveProductsAsync(Arg.Do<MarketImport>(market => resolved.Add(market)),
      Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(new ProductImportResult([], [], []));

    // Act
    await _handler.Handle(command, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(2, resolved.Single().Products.Single().Formats.Count);
  }

  [Fact]
  public async Task Handler_PassesPrimitiveMarketNamesToRepository() {
    // Arrange
    Command command = CreateCommand();
    IReadOnlyCollection<string>? requested = null;
    _marketRepository.ResolveMarketsAsync(Arg.Do<IReadOnlyCollection<string>>(names => requested = names), Arg.Any<CancellationToken>())
      .Returns(KnownMarkets());

    // Act
    await _handler.Handle(command, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(["Market"], requested);
  }

  [Fact]
  public async Task Handler_ResolvesEachBrandOnce() {
    // Arrange
    Command original = CreateCommand();
    CommandProduct product = original.Products.Single();
    Command command = original with { Products = [product, product with { Name = "Bread", BrandName = "BRAND" }] };
    IReadOnlyCollection<string>? requested = null;
    _brandRepository.ResolveBrandsAsync(Arg.Do<IReadOnlyCollection<string>>(names => requested = names), Arg.Any<CancellationToken>())
      .Returns(KnownBrands());

    // Act
    await _handler.Handle(command, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(["Brand"], requested);
  }

  [Fact]
  public async Task Handler_PreservesProductName() {
    // Arrange
    Command command = CreateCommand();
    MarketImport? resolved = null;
    _productRepository.ResolveProductsAsync(Arg.Do<MarketImport>(market => resolved = market), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
      .Returns(new ProductImportResult([], [], []));

    // Act
    await _handler.Handle(command, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal("Milk", resolved!.Products.Single().Name.Value);
  }

  [Fact]
  public async Task Handler_ConvertsQuantityToCanonicalUnit() {
    // Arrange
    Command command = CreateCommand();
    MarketImport? resolved = null;
    _productRepository.ResolveProductsAsync(Arg.Do<MarketImport>(market => resolved = market), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
      .Returns(new ProductImportResult([], [], []));

    // Act
    await _handler.Handle(command, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(Quantity.Create(1m, "l"), resolved!.Products.Single().Formats.Single().Quantity);
  }

  [Theory]
  [InlineData(28)]
  [InlineData(29)]
  public async Task Handler_SkipsUnchangedPrice(int observationDay) {
    // Arrange
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007"));
    var latestDate = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc);
    var observedAt = new DateTime(2026, 7, observationDay, 0, 0, 0, DateTimeKind.Utc);
    var observation = new PriceObservation(formatId, new Money(1.99m), observedAt);
    var latest = PriceSnapshot.Create(
      formatId, Money.Create(1.99m), ObservationTime.Create(latestDate));
    _productRepository.ResolveProductsAsync(Arg.Any<MarketImport>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
      .Returns(new ProductImportResult([], [], [observation]));
    _snapshotRepository.GetLatestForFormatsAsync(Arg.Any<IReadOnlyCollection<ProductFormatId>>(), Arg.Any<CancellationToken>()).Returns([latest]);
    IReadOnlyCollection<PriceObservation>? appended = null;
    _snapshotRepository.AppendAsync(Arg.Do<IReadOnlyCollection<PriceObservation>>(values => appended = values), Arg.Any<CancellationToken>())
      .Returns(Task.CompletedTask);
    Command command = CreateCommand() with { RegisteredAt = new DateOnly(2026, 7, observationDay) };

    // Act
    await _handler.Handle(command, TestContext.Current.CancellationToken);

    // Assert
    Assert.Empty(appended!);
  }

  [Theory]
  [InlineData(1.49)]
  [InlineData(2.49)]
  public async Task Handler_AppendsChangedPrice(decimal currentPrice) {
    // Arrange
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007"));
    var observedAt = new DateTime(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc);
    var observation = new PriceObservation(formatId, new Money(currentPrice), observedAt);
    var latest = PriceSnapshot.Create(
      formatId, Money.Create(1.99m), ObservationTime.Create(observedAt.AddDays(-1)));
    _productRepository.ResolveProductsAsync(Arg.Any<MarketImport>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
      .Returns(new ProductImportResult([], [], [observation]));
    _snapshotRepository.GetLatestForFormatsAsync(Arg.Any<IReadOnlyCollection<ProductFormatId>>(), Arg.Any<CancellationToken>()).Returns([latest]);
    IReadOnlyCollection<PriceObservation>? appended = null;
    _snapshotRepository.AppendAsync(Arg.Do<IReadOnlyCollection<PriceObservation>>(values => appended = values), Arg.Any<CancellationToken>())
      .Returns(Task.CompletedTask);
    Command command = CreateCommand() with { RegisteredAt = new DateOnly(2026, 7, 29) };

    // Act
    await _handler.Handle(command, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(observation, appended!.Single());
  }

  [Fact]
  public async Task Handler_AppendsFirstPrice() {
    // Arrange
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007"));
    var observedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc);
    var observation = new PriceObservation(formatId, new Money(1.99m), observedAt);
    _productRepository.ResolveProductsAsync(Arg.Any<MarketImport>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
      .Returns(new ProductImportResult([], [], [observation]));
    IReadOnlyCollection<PriceObservation>? appended = null;
    _snapshotRepository.AppendAsync(Arg.Do<IReadOnlyCollection<PriceObservation>>(values => appended = values), Arg.Any<CancellationToken>())
      .Returns(Task.CompletedTask);
    Command command = CreateCommand();

    // Act
    await _handler.Handle(command, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(observation, appended!.Single());
  }

  [Fact]
  public async Task Handler_RejectsUnknownMarket() {
    // Arrange
    _marketRepository.ResolveMarketsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid>());
    Command command = CreateCommand();

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    Assert.IsType<UnknownMarketException>(exception);
  }

  [Fact]
  public async Task Handler_DoesNotWriteProductsForUnknownMarket() {
    // Arrange
    _marketRepository.ResolveMarketsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid>());
    Command command = CreateCommand();

    // Act
    await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    await _productRepository.DidNotReceive().ResolveProductsAsync(Arg.Any<MarketImport>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handler_RejectsUnknownBrand() {
    // Arrange
    _brandRepository.ResolveBrandsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid>());
    Command command = CreateCommand();

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    Assert.IsType<UnknownProductBrandException>(exception);
  }

  [Fact]
  public async Task Handler_DoesNotWriteProductsForUnknownBrand() {
    // Arrange
    _brandRepository.ResolveBrandsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid>());
    Command command = CreateCommand();

    // Act
    await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    await _productRepository.DidNotReceive().ResolveProductsAsync(Arg.Any<MarketImport>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handler_UnknownBrandPreservesMarketName() {
    // Arrange
    _brandRepository.ResolveBrandsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid>());
    Command command = CreateCommand();

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));
    var failure = (UnknownProductBrandException)exception!;

    // Assert
    Assert.Equal("Market", failure.MarketName.Value);
  }

  [Fact]
  public async Task Handler_UnknownBrandPreservesProductName() {
    // Arrange
    _brandRepository.ResolveBrandsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid>());
    Command command = CreateCommand();

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));
    var failure = (UnknownProductBrandException)exception!;

    // Assert
    Assert.Equal("Milk", failure.ProductName.Value);
  }

  [Fact]
  public async Task Handler_UnknownBrandPreservesBrandName() {
    // Arrange
    _brandRepository.ResolveBrandsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid>());
    Command command = CreateCommand();

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));
    var failure = (UnknownProductBrandException)exception!;

    // Assert
    Assert.Equal("Brand", failure.BrandName.Value);
  }

  [Fact]
  public async Task Handler_UnknownBrandPreservesCode() {
    // Arrange
    _brandRepository.ResolveBrandsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid>());
    Command command = CreateCommand();

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));
    var failure = (UnknownProductBrandException)exception!;

    // Assert
    Assert.Equal("Ingestion.UnknownBrand", failure.Code);
  }

  [Fact]
  public async Task Handler_UnknownBrandRejectsWholeImport() {
    // Arrange
    Command original = CreateCommand();
    CommandProduct product = original.Products.Single();
    Command command = original with { Products = [product, product with { Name = "Bread", BrandName = "Unknown" }] };

    // Act
    await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    await _productRepository.DidNotReceive().ResolveProductsAsync(Arg.Any<MarketImport>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handler_Rollback_PropagatesCancellation() {
    // Arrange
    var productId = new ProductId(Guid.Parse("00000000-0000-7000-8000-000000000003"));
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007"));
    var observedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc);
    var observation = new PriceObservation(formatId, new Money(1.99m), observedAt);
    _productRepository.ResolveProductsAsync(Arg.Any<MarketImport>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
      .Returns(new ProductImportResult([productId], [formatId], [observation]));
    _snapshotRepository.AppendAsync(Arg.Any<IReadOnlyCollection<PriceObservation>>(), Arg.Any<CancellationToken>())
      .ThrowsAsync(new OperationCanceledException());
    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    _productRepository.DeleteProductsAsync(Arg.Any<IReadOnlyCollection<ProductId>>(), Arg.Any<CancellationToken>())
      .Returns(_ => {
        completed.TrySetResult();
        return Task.CompletedTask;
      });
    Command command = CreateCommand();

    // Act
    Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));
    await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<OperationCanceledException>(exception);
  }

  [Fact]
  public async Task Handler_Rollback_DeletesCreatedProducts() {
    // Arrange
    var productId = new ProductId(Guid.Parse("00000000-0000-7000-8000-000000000003"));
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007"));
    var observedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc);
    var observation = new PriceObservation(formatId, new Money(1.99m), observedAt);
    _productRepository.ResolveProductsAsync(Arg.Any<MarketImport>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
      .Returns(new ProductImportResult([productId], [formatId], [observation]));
    _snapshotRepository.AppendAsync(Arg.Any<IReadOnlyCollection<PriceObservation>>(), Arg.Any<CancellationToken>())
      .ThrowsAsync(new OperationCanceledException());
    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    _productRepository.DeleteProductsAsync(Arg.Any<IReadOnlyCollection<ProductId>>(), Arg.Any<CancellationToken>())
      .Returns(_ => {
        completed.TrySetResult();
        return Task.CompletedTask;
      });
    Command command = CreateCommand();

    // Act
    await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));
    await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    // Assert
    await _productRepository.Received(1).DeleteProductsAsync(Arg.Is<IReadOnlyCollection<ProductId>>(ids => ids.Single() == productId), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handler_Rollback_DeletesCreatedFormats() {
    // Arrange
    var productId = new ProductId(Guid.Parse("00000000-0000-7000-8000-000000000003"));
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007"));
    var observedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc);
    var observation = new PriceObservation(formatId, new Money(1.99m), observedAt);
    _productRepository.ResolveProductsAsync(Arg.Any<MarketImport>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
      .Returns(new ProductImportResult([productId], [formatId], [observation]));
    _snapshotRepository.AppendAsync(Arg.Any<IReadOnlyCollection<PriceObservation>>(), Arg.Any<CancellationToken>())
      .ThrowsAsync(new OperationCanceledException());
    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    _productRepository.DeleteProductsAsync(Arg.Any<IReadOnlyCollection<ProductId>>(), Arg.Any<CancellationToken>())
      .Returns(_ => {
        completed.TrySetResult();
        return Task.CompletedTask;
      });
    Command command = CreateCommand();

    // Act
    await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));
    await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    // Assert
    await _productRepository.Received(1).DeleteProductFormatsAsync(Arg.Is<IReadOnlyCollection<ProductFormatId>>(ids => ids.Single() == formatId), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handler_Rollback_DeletesImportSnapshots() {
    // Arrange
    var productId = new ProductId(Guid.Parse("00000000-0000-7000-8000-000000000003"));
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007"));
    var observedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc);
    var observation = new PriceObservation(formatId, new Money(1.99m), observedAt);
    _productRepository.ResolveProductsAsync(Arg.Any<MarketImport>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
      .Returns(new ProductImportResult([productId], [formatId], [observation]));
    _snapshotRepository.AppendAsync(Arg.Any<IReadOnlyCollection<PriceObservation>>(), Arg.Any<CancellationToken>())
      .ThrowsAsync(new OperationCanceledException());
    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    _productRepository.DeleteProductsAsync(Arg.Any<IReadOnlyCollection<ProductId>>(), Arg.Any<CancellationToken>())
      .Returns(_ => {
        completed.TrySetResult();
        return Task.CompletedTask;
      });
    Command command = CreateCommand();

    // Act
    await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));
    await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    // Assert
    await _snapshotRepository.Received(1).DeleteForMarketsAsync(Arg.Is<IReadOnlyCollection<MarketName>>(names => names.Single().Value == "Market"), observedAt, Arg.Any<CancellationToken>());
  }

  public void Dispose() {
    _services.Dispose();
  }

  private static Dictionary<string, Guid> KnownMarkets() {
    return new Dictionary<string, Guid>(StringComparer.InvariantCultureIgnoreCase) {
      ["Market"] = Guid.Parse("00000000-0000-7000-8000-000000000001"),
      ["Other market"] = Guid.Parse("00000000-0000-7000-8000-000000000002"),
    };
  }

  private static Dictionary<string, Guid> KnownBrands() {
    return new Dictionary<string, Guid>(StringComparer.InvariantCultureIgnoreCase) {
      ["Brand"] = Guid.Parse("00000000-0000-7000-8000-000000000003"),
      ["Other brand"] = Guid.Parse("00000000-0000-7000-8000-000000000004"),
    };
  }

  private static Command CreateCommand() {
    var product = new CommandProduct("Milk", 1.99m, 1m, "l", "Market", "Brand", new Uri("https://example.test/milk.png"));
    return new Command([product], new DateOnly(2026, 7, 28));
  }
}