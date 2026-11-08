using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using NSubstitute;
using static Metaspesa.Application.Markets.AddMarketProducts;

namespace Metaspesa.Application.UnitTests.Markets;

public sealed class AddProductsHandlerTest {
  private readonly IMarketRepository _marketsRepo;
  private readonly IBrandRepository _brandsRepo;
  private readonly IProductRepository _productsRepo;

  private readonly Handler _handler;

  public AddProductsHandlerTest() {
    _marketsRepo = Substitute.For<IMarketRepository>();
    _brandsRepo = Substitute.For<IBrandRepository>();
    _productsRepo = Substitute.For<IProductRepository>();

    _handler = new Handler(_marketsRepo, _brandsRepo, _productsRepo);
  }

  [Fact]
  public async Task Handle_RejectsEmptyImport() {
    var command = new Command([], new DateOnly(2026, 7, 28));

    Task action() => _handler.Handle(command, TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<EmptyMarketProductsException>(action);
  }

  [Fact]
  public async Task Handle_RejectsUnsupportedDate() {
    var item = new CommandProduct(
      "Milk", 1.99m, 1, "l", "Market", "Brand", new Uri("https://example.com/milk.png"));
    var command = new Command([item], new DateOnly(2022, 12, 31));

    Task action() => _handler.Handle(command, TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<InvalidMarketProductsRegisteredAtException>(action);
  }

  [Fact]
  public async Task Handle_ReportsUnknownMarket() {
    var item = new CommandProduct(
      "Milk", 1.99m, 1, "l", "Market", "Brand", new Uri("https://example.com/milk.png"));
    var command = new Command([item], new DateOnly(2026, 7, 28));
    _marketsRepo.ResolveMarketsAsync(
        Arg.Is<IReadOnlyCollection<string>>(names => names.Single() == item.MarketName),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid>());

    Task action() => _handler.Handle(command, TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<UnknownMarketException>(action);
  }

  [Fact]
  public async Task Handle_ReportsUnknownBrand() {
    var item = new CommandProduct(
      "Milk", 1.99m, 1, "l", "Market", "Brand", new Uri("https://example.com/milk.png"));
    var command = new Command([item], new DateOnly(2026, 7, 28));

    _marketsRepo.ResolveMarketsAsync(
        Arg.Any<IReadOnlyCollection<string>>(),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { [item.MarketName!] = Guid.CreateVersion7() });
    _brandsRepo.ResolveBrandsAsync(
        Arg.Is<IReadOnlyCollection<string>>(names => names.Single() == item.BrandName),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid>());

    Task action() => _handler.Handle(command, TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<UnknownProductBrandException>(action);
  }

  [Fact]
  public async Task Handle_SavesNewAggregate() {
    var item = new CommandProduct(
      "Milk", 1.99m, 1, "l", "Market", "Brand", new Uri("https://example.com/milk.png"));
    var command = new Command([item], new DateOnly(2026, 7, 28));

    var identity = new ProductIdentity(
      new MarketId(Guid.CreateVersion7()),
      new BrandId(Guid.CreateVersion7()),
      ProductName.Create(item.Name!));

    _marketsRepo.ResolveMarketsAsync(
        Arg.Any<IReadOnlyCollection<string>>(),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { [item.MarketName!] = identity.MarketId.Value });
    _brandsRepo.ResolveBrandsAsync(
        Arg.Any<IReadOnlyCollection<string>>(),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { [item.BrandName!] = identity.BrandId.Value });
    _productsRepo.GetBatchAsync(
        Arg.Is<IReadOnlyCollection<ProductIdentity>>(p => p.Single() == identity),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<ProductIdentity, Product>());

    await _handler.Handle(command, TestContext.Current.CancellationToken);

    await _productsRepo.Received(1).SaveBatchAsync(
      Arg.Is<IReadOnlyCollection<Product>>(products =>
        products.Single().IsIdentifiedBy(
          identity.MarketId.Value, identity.BrandId.Value, identity.Name.Value)),
      TestContext.Current.CancellationToken);
  }

  [Fact]
  public async Task Handle_RegistersObservationOnLoadedAggregate() {
    var item = new CommandProduct(
      "Milk", 1.99m, 1, "l", "Market", "Brand", new Uri("https://example.com/milk.png"));
    var command = new Command([item], new DateOnly(2026, 7, 28));

    var identity = new ProductIdentity(
      new MarketId(Guid.CreateVersion7()),
      new BrandId(Guid.CreateVersion7()),
      ProductName.Create(item.Name!));

    _marketsRepo.ResolveMarketsAsync(
        Arg.Any<IReadOnlyCollection<string>>(),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { [item.MarketName!] = identity.MarketId.Value });
    _brandsRepo.ResolveBrandsAsync(
        Arg.Any<IReadOnlyCollection<string>>(),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { [item.BrandName!] = identity.BrandId.Value });

    var observation = new ProductObservationParams(
      identity.MarketId.Value,
      identity.BrandId.Value,
      identity.Name.Value,
      command.RegisteredAt.AddDays(-1),
      [new ProductFormatObservationParams(1, "l", 1.49m, item.ImageUrl!)]);
    var product = Product.Create(observation);
    _productsRepo.GetBatchAsync(
        Arg.Is<IReadOnlyCollection<ProductIdentity>>(p => p.Single() == identity),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<ProductIdentity, Product> { [identity] = product });

    await _handler.Handle(command, TestContext.Current.CancellationToken);

    await _productsRepo.Received(1).SaveBatchAsync(
      Arg.Is<IReadOnlyCollection<Product>>(p => p.Single().Id == product.Id),
      TestContext.Current.CancellationToken);
  }

  [Fact]
  public async Task Handle_NormalizesScraperUnitAliasBeforeRegistration() {
    var item = new CommandProduct(
      "Meat", 1.99m, 1, "GRAMOS", "Market", "Brand", new Uri("https://example.com/meat.png"));
    var command = new Command([item], new DateOnly(2026, 7, 28));
    var formatObservation = new ProductFormatObservationParams(
      item.Quantity, "g", item.Price, item.ImageUrl!
    );

    var identity = new ProductIdentity(
      new MarketId(Guid.CreateVersion7()),
      new BrandId(Guid.CreateVersion7()),
      ProductName.Create(item.Name!));

    _marketsRepo.ResolveMarketsAsync(
        Arg.Any<IReadOnlyCollection<string>>(),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { [item.MarketName!] = identity.MarketId.Value });
    _brandsRepo.ResolveBrandsAsync(
        Arg.Any<IReadOnlyCollection<string>>(),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { [item.BrandName!] = identity.BrandId.Value });
    _productsRepo.GetBatchAsync(
        Arg.Is<IReadOnlyCollection<ProductIdentity>>(p => p.Single() == identity),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<ProductIdentity, Product>());

    await _handler.Handle(command, TestContext.Current.CancellationToken);

    await _productsRepo.Received(1).SaveBatchAsync(
      Arg.Is<IReadOnlyCollection<Product>>(p => p.Single().OffersFormat(formatObservation)),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_GroupsFormatsByCaseInsensitiveIdentity() {
    var item = new CommandProduct(
      "Milk", 1.99m, 1, "l", "Market", "Brand", new Uri("https://example.com/milk.png"));
    var otherItem = new CommandProduct(
      "milk", 1.99m, 2, "l", "MARKET", " brand ", new Uri("https://example.com/milk2.png"));
    var command = new Command([item, otherItem], new DateOnly(2026, 7, 28));

    var identity = new ProductIdentity(
      new MarketId(Guid.CreateVersion7()),
      new BrandId(Guid.CreateVersion7()),
      ProductName.Create(item.Name!));

    _marketsRepo.ResolveMarketsAsync(
        Arg.Any<IReadOnlyCollection<string>>(),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { [item.MarketName!] = identity.MarketId.Value });
    _brandsRepo.ResolveBrandsAsync(
        Arg.Any<IReadOnlyCollection<string>>(),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { [item.BrandName!] = identity.BrandId.Value });
    _productsRepo.GetBatchAsync(
        Arg.Is<IReadOnlyCollection<ProductIdentity>>(p => p.Single() == identity),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<ProductIdentity, Product>());

    await _handler.Handle(command, TestContext.Current.CancellationToken);

    await _productsRepo.Received(1).SaveBatchAsync(
      Arg.Is<IReadOnlyCollection<Product>>(products =>
        products.Count == 1 && products.Single().CountFormats() == 2),
      TestContext.Current.CancellationToken);
  }

  [Fact]
  public async Task Handle_DoesNotSaveBatchWithInvalidLaterGroup() {
    var item = new CommandProduct(
      "Milk", 1.99m, 1, "l", "Market", "Brand", new Uri("https://example.com/milk.png"));
    var command = new Command(
      [item, item with { Name = "Yogurt" }, item with { Name = "Yogurt" }],
      new DateOnly(2026, 7, 28));

    var identity = new ProductIdentity(
      new MarketId(Guid.CreateVersion7()),
      new BrandId(Guid.CreateVersion7()),
      ProductName.Create(item.Name!));

    _marketsRepo.ResolveMarketsAsync(
        Arg.Any<IReadOnlyCollection<string>>(),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { [item.MarketName!] = identity.MarketId.Value });
    _brandsRepo.ResolveBrandsAsync(
        Arg.Any<IReadOnlyCollection<string>>(),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { [item.BrandName!] = identity.BrandId.Value });
    _productsRepo.GetBatchAsync(
        Arg.Is<IReadOnlyCollection<ProductIdentity>>(p => p.Single() == identity),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<ProductIdentity, Product>());

    _ = await Record.ExceptionAsync(() => _handler.Handle(
      command, TestContext.Current.CancellationToken));

    await _productsRepo.DidNotReceive().SaveBatchAsync(
      Arg.Any<IReadOnlyCollection<Product>>(),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_SavesBatchesOfProductGroups() {
    const int BatchSize = 1000;
    var item = new CommandProduct(
      "Milk", 1.99m, 1, "l", "Market", "Brand", new Uri("https://example.com/milk.png"));
    var command = new Command(
      [.. Enumerable.Range(0, BatchSize + 1).Select(index => item with { Name = $"Product {index}" })],
      new DateOnly(2026, 7, 28));

    var identity = new ProductIdentity(
      new MarketId(Guid.CreateVersion7()),
      new BrandId(Guid.CreateVersion7()),
      ProductName.Create(item.Name!));

    _marketsRepo.ResolveMarketsAsync(
        Arg.Any<IReadOnlyCollection<string>>(),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { [item.MarketName!] = identity.MarketId.Value });
    _brandsRepo.ResolveBrandsAsync(
        Arg.Any<IReadOnlyCollection<string>>(),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { [item.BrandName!] = identity.BrandId.Value });
    _productsRepo.GetBatchAsync(
        Arg.Is<IReadOnlyCollection<ProductIdentity>>(p => p.Single() == identity),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<ProductIdentity, Product>());

    var sizes = new List<int>();
    _productsRepo.SaveBatchAsync(
        Arg.Do<IReadOnlyCollection<Product>>(products => sizes.Add(products.Count)),
        Arg.Any<CancellationToken>())
      .Returns(Task.CompletedTask);

    await _handler.Handle(command, TestContext.Current.CancellationToken);

    Assert.Equal([BatchSize, 1], sizes);
  }

  [Fact]
  public async Task Handle_DoesNotSplitProductGroupAcrossBatches() {
    const int BatchSize = 1000;
    var item = new CommandProduct(
      "Milk", 1.99m, 1, "l", "Market", "Brand", new Uri("https://example.com/milk.png"));
    var command = new Command(
      [..
        Enumerable.Range(0, BatchSize).Select(index => item with { Name = $"Product {index}" }),
        item with { Name = "Product 999", Quantity = 2 }
      ],
      new DateOnly(2026, 7, 28));

    var identity = new ProductIdentity(
      new MarketId(Guid.CreateVersion7()),
      new BrandId(Guid.CreateVersion7()),
      ProductName.Create(item.Name!));

    _marketsRepo.ResolveMarketsAsync(
        Arg.Any<IReadOnlyCollection<string>>(),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { [item.MarketName!] = identity.MarketId.Value });
    _brandsRepo.ResolveBrandsAsync(
        Arg.Any<IReadOnlyCollection<string>>(),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { [item.BrandName!] = identity.BrandId.Value });
    _productsRepo.GetBatchAsync(
        Arg.Is<IReadOnlyCollection<ProductIdentity>>(p => p.Single() == identity),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<ProductIdentity, Product>());

    IReadOnlyCollection<Product>? saved = null;
    _productsRepo.SaveBatchAsync(
        Arg.Do<IReadOnlyCollection<Product>>(products => saved = products),
        Arg.Any<CancellationToken>())
      .Returns(Task.CompletedTask);

    await _handler.Handle(command, TestContext.Current.CancellationToken);

    Product savedProduct = saved!.Single(product =>
      product.IsNamed("Product 999"));
    Assert.Equal(2, savedProduct.CountFormats());
  }

  [Fact]
  public async Task Handle_DoesNotSaveInvalidLaterBatch() {
    const int BatchSize = 1000;
    var item = new CommandProduct(
      "Milk", 1.99m, 1, "l", "Market", "Brand", new Uri("https://example.com/milk.png"));
    var command = new Command(
      [..
        Enumerable.Range(0, BatchSize + 1).Select(index => item with { Name = $"Product {index}" }),
        item with { Name = "Product 1000" }
      ],
      new DateOnly(2026, 7, 28));

    var identity = new ProductIdentity(
      new MarketId(Guid.CreateVersion7()),
      new BrandId(Guid.CreateVersion7()),
      ProductName.Create(item.Name!));

    _marketsRepo.ResolveMarketsAsync(
        Arg.Any<IReadOnlyCollection<string>>(),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { [item.MarketName!] = identity.MarketId.Value });
    _brandsRepo.ResolveBrandsAsync(
        Arg.Any<IReadOnlyCollection<string>>(),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { [item.BrandName!] = identity.BrandId.Value });
    _productsRepo.GetBatchAsync(
        Arg.Is<IReadOnlyCollection<ProductIdentity>>(p => p.Single() == identity),
        Arg.Any<CancellationToken>())
      .Returns(new Dictionary<ProductIdentity, Product>());

    var sizes = new List<int>();
    _productsRepo.SaveBatchAsync(
        Arg.Do<IReadOnlyCollection<Product>>(products => sizes.Add(products.Count)),
        Arg.Any<CancellationToken>())
      .Returns(Task.CompletedTask);

    _ = await Record.ExceptionAsync(() =>
      _handler.Handle(command, TestContext.Current.CancellationToken));

    Assert.Equal([1000], sizes);
  }

  [Fact]
  public async Task Handle_DoesNotLoadBatchesAfterInvalidGroup() {
    var marketId = Guid.CreateVersion7();
    var brandId = Guid.CreateVersion7();
    _marketsRepo.ResolveMarketsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { ["Market"] = marketId });
    _brandsRepo.ResolveBrandsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
      .Returns(new Dictionary<string, Guid> { ["Brand"] = brandId });
    _productsRepo.GetBatchAsync(Arg.Any<IReadOnlyCollection<ProductIdentity>>(), Arg.Any<CancellationToken>())
      .Returns(new Dictionary<ProductIdentity, Product>());
    var item = new CommandProduct("Milk", 1.99m, 1, "l", "Market", "Brand", new Uri("https://example.com/milk.png"));
    var command = new Command([item], new DateOnly(2026, 7, 28));
    command = command with {
      Products = [
      .. Enumerable.Range(0, 2001).Select(index => item with { Name = $"Product {index}" }),
      item with { Name = "Product 1000" }]
    };

    _ = await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    await _productsRepo.Received(2).GetBatchAsync(Arg.Any<IReadOnlyCollection<ProductIdentity>>(), Arg.Any<CancellationToken>());
  }
}