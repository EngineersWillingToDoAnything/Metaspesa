using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Metaspesa.Application.Markets;

public static class AddMarketProducts {
  public record CommandProduct(
    string? Name,
    decimal Price,
    decimal Quantity,
    string? UnitOfMeasure,
    string? MarketName,
    string? BrandName,
    Uri? ImageUrl
  );

  public record Command(
    IReadOnlyCollection<CommandProduct> Products,
    DateOnly RegisteredAt
  ) {
    internal List<MarketImport> ToMarkets() => [
      ..Products.GroupBy(product => new MarketName(product.MarketName!))
        .Select(marketGroup => new MarketImport(
          marketGroup.Key,
          [
            ..marketGroup.GroupBy(product => (
              Name: new ProductName(product.Name!),
              Brand: new BrandName(product.BrandName!)))
              .Select(productGroup => new ProductImport(
                productGroup.Key.Name,
                productGroup.Key.Brand,
                [
                  ..productGroup.Select(product => new ProductFormatImport(
                    Quantity.Create(
                      product.Quantity,
                      UnitOfMeasureNormalizer.Normalize(product.UnitOfMeasure)!),
                    new Money(product.Price),
                    product.ImageUrl is null ? null : new ImageUrl(product.ImageUrl)))
                ]))
          ]))
    ];
  }

  public class Handler(
    IMarketRepository marketRepository,
    IBrandRepository brandRepository,
    IProductRepository productRepository,
    IPriceSnapshotRepository priceSnapshotRepository,
    IServiceScopeFactory scopeFactory,
    ILogger<Handler> logger
  ) : CancellableCommandHandler<Command>(scopeFactory, logger) {
    private readonly List<ProductId> _addedProductIds = [];
    private readonly List<ProductFormatId> _addedProductFormatIds = [];
    private readonly List<MarketName> _completedMarkets = [];

    protected override bool HasRollbackWork =>
      _completedMarkets.Count > 0 ||
      _addedProductIds.Count > 0 ||
      _addedProductFormatIds.Count > 0;

    protected override async Task ExecuteAsync(
      Command command,
      CancellationToken cancellationToken
    ) {
      ArgumentNullException.ThrowIfNull(command);
      ArgumentNullException.ThrowIfNull(command.Products);

      if (command.Products.Count == 0) {
        throw new EmptyMarketProductsException();
      }
      if (command.RegisteredAt < new DateOnly(2023, 1, 1)) {
        throw new InvalidMarketProductsRegisteredAtException(
          command.RegisteredAt);
      }

      List<MarketImport> markets = command.ToMarkets();
      EnsureProductsAreUnique(markets);

      await EnsureMarketsExistAsync(markets, cancellationToken);
      await EnsureBrandsExistAsync(markets, cancellationToken);

      await AddProductsAndSnapshotsAsync(command, markets, cancellationToken);
    }

    private static void EnsureProductsAreUnique(IEnumerable<MarketImport> markets) {
      var identities = new HashSet<(MarketName Market, string Name, BrandName Brand, Quantity Quantity)>();
      foreach (MarketImport market in markets) {
        foreach (ProductImport product in market.Products) {
          ProductFormatImport? duplicate = product.Formats.FirstOrDefault(format =>
            !identities.Add((market.Name,
              product.Name.Value.ToUpperInvariant(), product.Brand,
              format.Quantity)));
          if (duplicate is not null) {
            throw new DuplicateMarketProductException(
              product.Name.Value, market.Name.Value, product.Brand.Value,
              (float)duplicate.Quantity.Amount, duplicate.Quantity.UnitOfMeasure.Value);
          }
        }
      }
    }

    private async Task EnsureBrandsExistAsync(
      IReadOnlyCollection<MarketImport> markets,
      CancellationToken cancellationToken
    ) {
      IReadOnlyDictionary<string, Guid> brandIds =
        await brandRepository.ResolveBrandsAsync([..
          markets.SelectMany(market => market.Products)
            .Select(product => product.Brand.Value)
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
        ], cancellationToken);

      foreach (MarketImport market in markets) {
        ProductImport? unknownProduct = market.Products.FirstOrDefault(
          product => !brandIds.ContainsKey(product.Brand.Value));
        if (unknownProduct is not null) {
          throw new UnknownProductBrandException(
            market.Name, unknownProduct.Name, unknownProduct.Brand);
        }
      }
    }

    private async Task EnsureMarketsExistAsync(
      IReadOnlyCollection<MarketImport> markets,
      CancellationToken cancellationToken
    ) {
      IReadOnlyDictionary<string, Guid> marketIds =
        await marketRepository.ResolveMarketsAsync(
          [.. markets.Select(market => market.Name.Value)], cancellationToken);

      MarketImport? unknownMarket = markets.FirstOrDefault(market =>
        !marketIds.ContainsKey(market.Name.Value));
      if (unknownMarket is not null) {
        throw new UnknownMarketException(unknownMarket.Name);
      }
    }

    private async Task AddProductsAndSnapshotsAsync(
      Command command,
      IReadOnlyCollection<MarketImport> markets,
      CancellationToken cancellationToken
    ) {
      var observedAt = command.RegisteredAt.ToDateTime(
        TimeOnly.MinValue,
        DateTimeKind.Utc);

      foreach (MarketImport market in markets) {
        ProductImportResult result = await productRepository.ResolveProductsAsync(
          market,
          observedAt,
          cancellationToken);
        _addedProductIds.AddRange(result.AddedProductIds);
        _addedProductFormatIds.AddRange(result.AddedProductFormatIds);
        _completedMarkets.Add(market.Name);
        IReadOnlyCollection<PriceSnapshot> latestSnapshots =
          await priceSnapshotRepository.GetLatestForFormatsAsync(
            [.. result.PriceObservations.Select(observation => observation.ProductFormatId)],
            cancellationToken);
        var snapshotsByFormat = latestSnapshots.ToDictionary(snapshot => snapshot.ProductFormatId);
        List<PriceObservation> changedPrices = [
          .. result.PriceObservations.Where(observation =>
            !snapshotsByFormat.TryGetValue(observation.ProductFormatId, out PriceSnapshot? latest) ||
            !latest.HasSamePrice(observation.Price))
        ];
        await priceSnapshotRepository.AppendAsync(
          changedPrices,
          cancellationToken);
      }
    }

    protected override async Task RollbackAsync(
      Command command,
      IServiceProvider services,
      CancellationToken cancellationToken
    ) {
      ArgumentNullException.ThrowIfNull(command);

      IProductRepository productRepo = services.GetRequiredService<IProductRepository>();
      IPriceSnapshotRepository snapshotRepo =
        services.GetRequiredService<IPriceSnapshotRepository>();

      if (_completedMarkets.Count > 0) {
        var observedAt = command.RegisteredAt.ToDateTime(
          TimeOnly.MinValue,
          DateTimeKind.Utc);
        await snapshotRepo.DeleteForMarketsAsync(
          _completedMarkets,
          observedAt,
          cancellationToken);
      }
      if (_addedProductFormatIds.Count > 0) {
        await productRepo.DeleteProductFormatsAsync(
          _addedProductFormatIds,
          cancellationToken);
      }
      if (_addedProductIds.Count > 0) {
        await productRepo.DeleteProductsAsync(
          _addedProductIds,
          cancellationToken);
      }
    }
  }
}