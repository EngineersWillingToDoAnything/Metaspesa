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

public class AddProductsHandlerTest {
  private readonly IMarketRepository _marketRepository;
  private readonly IProductRepository _productRepository;
  private readonly IPriceSnapshotRepository _snapshotRepository;
  private readonly Handler _handler;

  public AddProductsHandlerTest() {
    _marketRepository = Substitute.For<IMarketRepository>();
    _productRepository = Substitute.For<IProductRepository>();
    _snapshotRepository = Substitute.For<IPriceSnapshotRepository>();
    _snapshotRepository.GetLatestForFormatsAsync(
      Arg.Any<IReadOnlyCollection<ProductFormatId>>(),
      Arg.Any<CancellationToken>())
      .Returns([]);

    IServiceScopeFactory scopeFactory = new ServiceCollection()
      .AddSingleton(_marketRepository)
      .AddSingleton(_productRepository)
      .AddSingleton(_snapshotRepository)
      .BuildServiceProvider()
      .GetRequiredService<IServiceScopeFactory>();

    _handler = new Handler(
      _marketRepository,
      _productRepository,
      _snapshotRepository,
      scopeFactory,
      Substitute.For<ILogger<Handler>>());

    _marketRepository.GetMarketsAsync(Arg.Any<CancellationToken>())
      .Returns([]);
    _productRepository.GetBrandsAsync(Arg.Any<CancellationToken>())
      .Returns([]);
    _productRepository
      .ResolveProductsAsync(
        Arg.Any<MarketImport>(),
        Arg.Any<DateTime>(),
        Arg.Any<CancellationToken>())
      .Returns(new ProductImportResult([], [], []));
  }

  [Fact(DisplayName = "Throws exact exception for an empty product import")]
  public async Task Handler_ThrowsEmptyMarketProducts_WhenImportIsEmpty() {
    async Task action() => await _handler.Handle(
      new Command([], new DateOnly(2026, 7, 28)),
      TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<EmptyMarketProductsException>(action);
    await _marketRepository.DidNotReceive().AddMarketsAsync(
      Arg.Any<IReadOnlyCollection<MarketName>>(),
      Arg.Any<CancellationToken>());
    await _productRepository.DidNotReceive().ResolveProductsAsync(
      Arg.Any<MarketImport>(),
      Arg.Any<DateTime>(),
      Arg.Any<CancellationToken>());
  }

  [Fact(DisplayName = "Throws exact exception for an old registration date")]
  public async Task Handler_ThrowsInvalidRegisteredAt_WhenDateIsTooOld() {
    Command command = CreateCommand() with {
      RegisteredAt = new DateOnly(2022, 12, 31),
    };

    async Task action() => await _handler.Handle(
      command,
      TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<InvalidMarketProductsRegisteredAtException>(
      action);
  }

  [Fact(DisplayName = "Throws exact exception for a duplicate product identity")]
  public async Task Handler_ThrowsDuplicateMarketProduct_WhenIdentityRepeats() {
    Command command = CreateCommand();
    command = command with {
      Products = [command.Products.Single(), command.Products.Single()],
    };

    async Task action() => await _handler.Handle(
      command,
      TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<DuplicateMarketProductException>(action);
  }

  [Fact(DisplayName = "Doesn't throw exact exception if products only differ in name")]
  public async Task Handler_DoesNotThrowDuplicateMarketProduct_WhenNamesDiffer() {
    Command command = CreateCommand();
    CommandProduct product = command.Products.Single();
    command = command with {
      Products = [
        product,
        product with { Name = "Bread" },
      ],
    };

    async Task action() => await _handler.Handle(
      command,
      TestContext.Current.CancellationToken);

    await action();
    Assert.True(true, "No exception was thrown");
  }

  [Fact(DisplayName = "Throws exact exception if products differ only in case of name")]
  public async Task Handler_ThrowsDuplicateMarketProduct_WhenNamesDifferOnlyInCase() {
    Command command = CreateCommand();
    CommandProduct product = command.Products.Single();
    command = command with {
      Products = [
        product,
        product with { Name = product.Name?.ToUpperInvariant() },
      ],
    };

    async Task action() => await _handler.Handle(
      command,
      TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<DuplicateMarketProductException>(action);
  }

  [Fact(DisplayName = "Doesn't throw exact exception if products only differ in market")]
  public async Task Handler_DoesNotThrowDuplicateMarketProduct_WhenMarketsDiffer() {
    Command command = CreateCommand();
    CommandProduct product = command.Products.Single();
    command = command with {
      Products = [
        product,
        product with { MarketName = "Other market" },
      ],
    };

    async Task action() => await _handler.Handle(
      command,
      TestContext.Current.CancellationToken);

    await action();
    Assert.True(true, "No exception was thrown");
  }

  [Fact(DisplayName = "Throws exact exception if products differ only in case of market")]
  public async Task Handler_ThrowsDuplicateMarketProduct_WhenMarketsDifferOnlyInCase() {
    Command command = CreateCommand();
    CommandProduct product = command.Products.Single();
    command = command with {
      Products = [
        product,
        product with { MarketName = product.MarketName?.ToUpperInvariant() },
      ],
    };

    async Task action() => await _handler.Handle(
      command,
      TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<DuplicateMarketProductException>(action);
  }

  [Fact(DisplayName = "Doesn't throw exact exception if products only differ in brand")]
  public async Task Handler_DoesNotThrowDuplicateMarketProduct_WhenBrandsDiffer() {
    Command command = CreateCommand();
    CommandProduct product = command.Products.Single();
    command = command with {
      Products = [
        product,
        product with { BrandName = "Other brand" },
      ],
    };

    async Task action() => await _handler.Handle(
      command,
      TestContext.Current.CancellationToken);

    await action();
    Assert.True(true, "No exception was thrown");
  }

  [Fact(DisplayName = "Throws exact exception if products differ only in case of brand")]
  public async Task Handler_ThrowsDuplicateMarketProduct_WhenBrandsDifferOnlyInCase() {
    Command command = CreateCommand();
    CommandProduct product = command.Products.Single();
    command = command with {
      Products = [
        product,
        product with { BrandName = product.BrandName?.ToUpperInvariant() },
      ],
    };

    async Task action() => await _handler.Handle(
      command,
      TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<DuplicateMarketProductException>(action);
  }

  [Fact(DisplayName = "Doesn't throw exact exception if products only differ in quantity")]
  public async Task Handler_DoesNotThrowDuplicateMarketProduct_WhenQuantitiesDiffer() {
    Command command = CreateCommand();
    CommandProduct product = command.Products.Single();
    command = command with {
      Products = [
        product,
        product with { Quantity = product.Quantity + 1 },
      ],
    };

    async Task action() => await _handler.Handle(
      command,
      TestContext.Current.CancellationToken);

    await action();
    Assert.True(true, "No exception was thrown");
  }

  [Fact(DisplayName = "Preserves distinct quantities at database precision")]
  public async Task Handler_AcceptsDistinctQuantities_AtDatabasePrecision() {
    Command command = CreateCommand();
    CommandProduct product = command.Products.Single();
    command = command with {
      Products = [
        product,
        product with { Quantity = product.Quantity + 0.001m },
      ],
    };

    await _handler.Handle(command, TestContext.Current.CancellationToken);

    await _productRepository.Received(1).ResolveProductsAsync(
      Arg.Is<MarketImport>(market => market.Products.Single().Formats.Count == 2),
      Arg.Any<DateTime>(),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handler_RejectsEquivalentQuantities_AfterUnitConversion() {
    Command command = CreateCommand();
    CommandProduct product = command.Products.Single();
    command = command with {
      Products = [product, product with { Quantity = 1000, UnitOfMeasure = "ml" }],
    };

    Task action() => _handler.Handle(command, TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<DuplicateMarketProductException>(action);
  }


  [Fact(DisplayName = "Doesn't throw exact exception if products only differ in unit of measure")]
  public async Task Handler_DoesNotThrowDuplicateMarketProduct_WhenUnitsDiffer() {
    Command command = CreateCommand();
    CommandProduct product = command.Products.Single();
    command = command with {
      Products = [
        product,
        product with { UnitOfMeasure = "kg" },
      ],
    };

    await _handler.Handle(command, TestContext.Current.CancellationToken);

    Assert.True(true, "No exception was thrown");
  }

  [Fact(DisplayName = "Throws exact exception if products differ only in case of unit of measure")]
  public async Task Handler_ThrowsDuplicateMarketProduct_WhenUnitsDifferOnlyInCase() {
    Command command = CreateCommand();
    CommandProduct product = command.Products.Single();
    command = command with {
      Products = [
        product,
        product with { UnitOfMeasure = product.UnitOfMeasure?.ToUpperInvariant() },
      ],
    };

    async Task action() => await _handler.Handle(
      command,
      TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<DuplicateMarketProductException>(action);
  }

  [Fact(DisplayName = "Throws exact exception if products differ only in image url ")]
  public async Task Handler_ThrowsDuplicateMarketProduct_WhenImageUrlsDiffer() {
    Command command = CreateCommand();
    CommandProduct product = command.Products.Single();
    command = command with {
      Products = [
        product,
        product with { ImageUrl = new Uri("https://example.com/other.png") },
      ],
    };

    async Task action() => await _handler.Handle(
      command,
      TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<DuplicateMarketProductException>(action);
  }

  [Fact(DisplayName = "Throws exact exception if products differ only in price ")]
  public async Task Handler_ThrowsDuplicateMarketProduct_WhenPricesDiffer() {
    Command command = CreateCommand();
    CommandProduct product = command.Products.Single();
    command = command with {
      Products = [
        product,
        product with { Price = product.Price + 1 },
      ],
    };

    async Task action() => await _handler.Handle(
      command,
      TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<DuplicateMarketProductException>(action);
  }

  [Fact(DisplayName = "Preserves exact domain exception for invalid product name")]
  public async Task Handler_ThrowsInvalidProductName_WhenNameIsInvalid() {
    Command command = CreateCommand();
    command = command with {
      Products = [command.Products.Single() with { Name = null }],
    };

    async Task action() => await _handler.Handle(
      command,
      TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<InvalidProductNameException>(action);
  }

  [Fact(DisplayName = "Builds validated imports and appends observations separately")]
  public async Task Handler_ResolvesProducts_AndAppendsSnapshots() {
    Command command = CreateCommand();
    var observation = new PriceObservation(
      new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007")),
      new Money(1.99m),
      new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc));
    _productRepository
      .ResolveProductsAsync(
        Arg.Any<MarketImport>(),
        Arg.Any<DateTime>(),
        Arg.Any<CancellationToken>())
      .Returns(new ProductImportResult(
        [new ProductId(Guid.Parse("00000000-0000-7000-8000-000000000003"))],
        [new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007"))],
        [observation]));

    await _handler.Handle(command, TestContext.Current.CancellationToken);
    await _marketRepository.Received(1).AddMarketsAsync(
      Arg.Is<IReadOnlyCollection<MarketName>>(names =>
        names.Single().Value == "Market"),
      TestContext.Current.CancellationToken);
    await _productRepository.Received(1).AddBrandsAsync(
      Arg.Is<IReadOnlyCollection<BrandName>>(brands =>
        brands.Single().Value == "Brand"),
      TestContext.Current.CancellationToken);
    await _productRepository.Received(1).ResolveProductsAsync(
      Arg.Is<MarketImport>(market =>
        market.Name.Value == "Market" &&
        market.Products.Single().Name.Value == "Milk" &&
        market.Products.Single().Formats.Single().Quantity.Amount == 1000m &&
        market.Products.Single().Formats.Single().Price.Amount == 1.99m),
      Arg.Is<DateTime>(value =>
        value == new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc)),
      TestContext.Current.CancellationToken);
    await _snapshotRepository.Received(1).AppendAsync(
      Arg.Is<IReadOnlyCollection<PriceObservation>>(values =>
        values.Single() == observation),
      TestContext.Current.CancellationToken);
  }

  [Fact(DisplayName = "Does not append snapshot when same-day price is unchanged")]
  public async Task Handler_AppendsNoSnapshot_WhenSameDayPriceIsUnchanged() {
    DateOnly registeredAt = new(2026, 7, 28);
    var observedAt = registeredAt.ToDateTime(
      TimeOnly.MinValue, DateTimeKind.Utc);
    var observation = new PriceObservation(
      new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007")), new Money(1.99m), observedAt);
    var latestSnapshot = new PriceSnapshot(
      new PriceSnapshotId(Guid.Parse("00000000-0000-7000-8000-000000000001")),
      observation.ProductFormatId,
      new Money(1.99m),
      observedAt);
    _productRepository.ResolveProductsAsync(
      Arg.Any<MarketImport>(), observedAt, Arg.Any<CancellationToken>())
      .Returns(new ProductImportResult([], [], [observation]));
    _snapshotRepository.GetLatestForFormatsAsync(
      Arg.Any<IReadOnlyCollection<ProductFormatId>>(),
      TestContext.Current.CancellationToken)
      .Returns([latestSnapshot]);

    await _handler.Handle(
      CreateCommand() with { RegisteredAt = registeredAt },
      TestContext.Current.CancellationToken);

    await _snapshotRepository.Received(1).AppendAsync(
      Arg.Is<IReadOnlyCollection<PriceObservation>>(values => values.Count == 0),
      TestContext.Current.CancellationToken);
  }

  [Fact(DisplayName = "Does not append snapshot when only observation date changes")]
  public async Task Handler_AppendsNoSnapshot_WhenOnlyObservationDateChanges() {
    DateOnly registeredAt = new(2026, 7, 29);
    var observedAt = registeredAt.ToDateTime(
      TimeOnly.MinValue, DateTimeKind.Utc);
    var observation = new PriceObservation(
      new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007")), new Money(1.99m), observedAt);
    var latestSnapshot = new PriceSnapshot(
      new PriceSnapshotId(Guid.Parse("00000000-0000-7000-8000-000000000001")),
      observation.ProductFormatId,
      new Money(1.99m),
      new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc));
    _productRepository.ResolveProductsAsync(
      Arg.Any<MarketImport>(), observedAt, Arg.Any<CancellationToken>())
      .Returns(new ProductImportResult([], [], [observation]));
    _snapshotRepository.GetLatestForFormatsAsync(
      Arg.Any<IReadOnlyCollection<ProductFormatId>>(),
      TestContext.Current.CancellationToken)
      .Returns([latestSnapshot]);

    await _handler.Handle(
      CreateCommand() with { RegisteredAt = registeredAt },
      TestContext.Current.CancellationToken);

    await _snapshotRepository.Received(1).AppendAsync(
      Arg.Is<IReadOnlyCollection<PriceObservation>>(values => values.Count == 0),
      TestContext.Current.CancellationToken);
  }

  [Fact(DisplayName = "Appends snapshot when price increases")]
  public async Task Handler_AppendsSnapshot_WhenPriceIncreases() {
    DateOnly registeredAt = new(2026, 7, 29);
    var observedAt = registeredAt.ToDateTime(
      TimeOnly.MinValue, DateTimeKind.Utc);
    var observation = new PriceObservation(
      new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007")), new Money(2.49m), observedAt);
    var latestSnapshot = new PriceSnapshot(
      new PriceSnapshotId(Guid.Parse("00000000-0000-7000-8000-000000000001")),
      observation.ProductFormatId,
      new Money(1.99m),
      new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc));
    _productRepository.ResolveProductsAsync(
      Arg.Any<MarketImport>(), observedAt, Arg.Any<CancellationToken>())
      .Returns(new ProductImportResult([], [], [observation]));
    _snapshotRepository.GetLatestForFormatsAsync(
      Arg.Any<IReadOnlyCollection<ProductFormatId>>(),
      TestContext.Current.CancellationToken)
      .Returns([latestSnapshot]);
    Command command = CreateCommand();
    command = command with {
      RegisteredAt = registeredAt,
      Products = [command.Products.Single() with { Price = 2.49m }],
    };

    await _handler.Handle(command, TestContext.Current.CancellationToken);

    await _snapshotRepository.Received(1).AppendAsync(
      Arg.Is<IReadOnlyCollection<PriceObservation>>(values =>
        values.Count == 1 && values.Single() == observation),
      TestContext.Current.CancellationToken);
  }

  [Fact(DisplayName = "Appends snapshot when price decreases")]
  public async Task Handler_AppendsSnapshot_WhenPriceDecreases() {
    DateOnly registeredAt = new(2026, 7, 29);
    var observedAt = registeredAt.ToDateTime(
      TimeOnly.MinValue, DateTimeKind.Utc);
    var observation = new PriceObservation(
      new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007")), new Money(1.49m), observedAt);
    var latestSnapshot = new PriceSnapshot(
      new PriceSnapshotId(Guid.Parse("00000000-0000-7000-8000-000000000001")),
      observation.ProductFormatId,
      new Money(1.99m),
      new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc));
    _productRepository.ResolveProductsAsync(
      Arg.Any<MarketImport>(), observedAt, Arg.Any<CancellationToken>())
      .Returns(new ProductImportResult([], [], [observation]));
    _snapshotRepository.GetLatestForFormatsAsync(
      Arg.Any<IReadOnlyCollection<ProductFormatId>>(),
      TestContext.Current.CancellationToken)
      .Returns([latestSnapshot]);
    Command command = CreateCommand();
    command = command with {
      RegisteredAt = registeredAt,
      Products = [command.Products.Single() with { Price = 1.49m }],
    };

    await _handler.Handle(command, TestContext.Current.CancellationToken);

    await _snapshotRepository.Received(1).AppendAsync(
      Arg.Is<IReadOnlyCollection<PriceObservation>>(values =>
        values.Count == 1 && values.Single() == observation),
      TestContext.Current.CancellationToken);
  }

  [Fact(DisplayName = "Appends first snapshot when format has no price history")]
  public async Task Handler_AppendsSnapshot_WhenFormatHasNoPriceHistory() {
    DateOnly registeredAt = new(2026, 7, 28);
    var observedAt = registeredAt.ToDateTime(
      TimeOnly.MinValue, DateTimeKind.Utc);
    var observation = new PriceObservation(
      new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007")), new Money(1.99m), observedAt);
    _productRepository.ResolveProductsAsync(
      Arg.Any<MarketImport>(), observedAt, Arg.Any<CancellationToken>())
      .Returns(new ProductImportResult([], [], [observation]));
    _snapshotRepository.GetLatestForFormatsAsync(
      Arg.Any<IReadOnlyCollection<ProductFormatId>>(),
      TestContext.Current.CancellationToken)
      .Returns([]);

    await _handler.Handle(
      CreateCommand() with { RegisteredAt = registeredAt },
      TestContext.Current.CancellationToken);

    await _snapshotRepository.Received(1).AppendAsync(
      Arg.Is<IReadOnlyCollection<PriceObservation>>(values =>
        values.Count == 1 && values.Single() == observation),
      TestContext.Current.CancellationToken);
  }

  [Fact(DisplayName = "Does not add existing market or brand")]
  public async Task Handler_DoesNotAddExistingMarketOrBrand() {
    _marketRepository.GetMarketsAsync(Arg.Any<CancellationToken>())
      .Returns([
        new Market(new MarketId(Guid.Parse("00000000-0000-7000-8000-000000000001")), new MarketName("MARKET")),
      ]);
    _productRepository.GetBrandsAsync(Arg.Any<CancellationToken>())
      .Returns([new BrandName("BRAND")]);

    await _handler.Handle(
      CreateCommand(), TestContext.Current.CancellationToken);

    await _marketRepository.DidNotReceive().AddMarketsAsync(
      Arg.Any<IReadOnlyCollection<MarketName>>(),
      Arg.Any<CancellationToken>());
    await _productRepository.DidNotReceive().AddBrandsAsync(
      Arg.Any<IReadOnlyCollection<BrandName>>(),
      Arg.Any<CancellationToken>());
  }

  [Fact(DisplayName = "Rolls back records created before cancellation")]
  public async Task Handler_RollsBack_WhenSnapshotAppendIsCancelled() {
    var result = new ProductImportResult(
      [new ProductId(Guid.Parse("00000000-0000-7000-8000-000000000003"))],
      [new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007"))],
      [new PriceObservation(
        new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007")),
        new Money(1.99m),
        new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc))]);
    _productRepository
      .ResolveProductsAsync(
        Arg.Any<MarketImport>(),
        Arg.Any<DateTime>(),
        Arg.Any<CancellationToken>())
      .Returns(result);
    _snapshotRepository
      .AppendAsync(
        Arg.Any<IReadOnlyCollection<PriceObservation>>(),
        Arg.Any<CancellationToken>())
      .ThrowsAsync<OperationCanceledException>();
    var rollbackSignal = new TaskCompletionSource(
      TaskCreationOptions.RunContinuationsAsynchronously);
    _marketRepository
      .DeleteMarketsAsync(
        Arg.Any<IReadOnlyCollection<MarketName>>(),
        Arg.Any<CancellationToken>())
      .Returns(_ => {
        rollbackSignal.SetResult();
        return Task.CompletedTask;
      });

    await Assert.ThrowsAsync<OperationCanceledException>(
      () => _handler.Handle(
        CreateCommand(), TestContext.Current.CancellationToken));
    await rollbackSignal.Task.WaitAsync(
      TimeSpan.FromSeconds(1),
      TestContext.Current.CancellationToken);

    await _snapshotRepository.Received(1).DeleteForMarketsAsync(
      Arg.Is<IReadOnlyCollection<MarketName>>(markets =>
        markets.Single().Value == "Market"),
      Arg.Any<DateTime>(),
      Arg.Any<CancellationToken>());
    await _productRepository.Received(1).DeleteProductsAsync(
      Arg.Is<IReadOnlyCollection<ProductId>>(ids =>
        ids.Single() == new ProductId(Guid.Parse("00000000-0000-7000-8000-000000000003"))),
      Arg.Any<CancellationToken>());
    await _productRepository.Received(1).DeleteProductFormatsAsync(
      Arg.Is<IReadOnlyCollection<ProductFormatId>>(ids =>
        ids.Single() == new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000007"))),
      Arg.Any<CancellationToken>());
    await _productRepository.Received(1).DeleteBrandsAsync(
      Arg.Any<IReadOnlyCollection<BrandName>>(),
      Arg.Any<CancellationToken>());
    await _marketRepository.Received(1).DeleteMarketsAsync(
      Arg.Any<IReadOnlyCollection<MarketName>>(),
      Arg.Any<CancellationToken>());
  }

  [Fact(DisplayName = "Rollback deletes only market and brand created by import")]
  public async Task Handler_RollbackPreservesExistingMarketAndBrand() {
    _marketRepository.GetMarketsAsync(Arg.Any<CancellationToken>())
      .Returns([new Market(new MarketId(Guid.Parse("00000000-0000-7000-8000-000000000001")), new MarketName("Existing"))]);
    _productRepository.GetBrandsAsync(Arg.Any<CancellationToken>())
      .Returns([new BrandName("Existing brand")]);
    _snapshotRepository.AppendAsync(
      Arg.Any<IReadOnlyCollection<PriceObservation>>(),
      Arg.Any<CancellationToken>())
      .ThrowsAsync<OperationCanceledException>();
    var rollbackSignal = new TaskCompletionSource(
      TaskCreationOptions.RunContinuationsAsynchronously);
    _marketRepository.DeleteMarketsAsync(
      Arg.Any<IReadOnlyCollection<MarketName>>(),
      Arg.Any<CancellationToken>())
      .Returns(_ => {
        rollbackSignal.SetResult();
        return Task.CompletedTask;
      });
    Command command = new([
      new CommandProduct(
        "Milk", 1.99m, 1, "l", "Existing", "Existing brand", null),
      new CommandProduct(
        "Bread", 2.49m, 1, "kg", "New market", "New brand", null),
    ], new DateOnly(2026, 7, 28));

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
      _handler.Handle(command, TestContext.Current.CancellationToken));
    await rollbackSignal.Task.WaitAsync(
      TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

    await _marketRepository.Received(1).DeleteMarketsAsync(
      Arg.Is<IReadOnlyCollection<MarketName>>(names =>
        names.Count == 1 && names.Single() == new MarketName("New market")),
      Arg.Any<CancellationToken>());
    await _productRepository.Received(1).DeleteBrandsAsync(
      Arg.Is<IReadOnlyCollection<BrandName>>(names =>
        names.Count == 1 && names.Single() == new BrandName("New brand")),
      Arg.Any<CancellationToken>());
  }

  private static Command CreateCommand() => new(
    [new CommandProduct(
      "Milk",
      1.99m,
      1,
      "l",
      "Market",
      "Brand",
      new Uri("https://example.com/milk.png"))],
    new DateOnly(2026, 7, 28));
}