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
    IProductRepository productRepository,
    IPriceSnapshotRepository priceSnapshotRepository,
    IServiceScopeFactory scopeFactory,
    ILogger<Handler> logger
  ) : CancellableCommandHandler<Command>(scopeFactory, logger) {
    private List<MarketName> _addedMarkets = [];
    private List<BrandName> _addedBrands = [];
    private readonly List<ProductId> _addedProductIds = [];
    private readonly List<ProductFormatId> _addedProductFormatIds = [];
    private readonly List<MarketName> _completedMarkets = [];

    protected override bool HasRollbackWork =>
      _completedMarkets.Count > 0 ||
      _addedProductIds.Count > 0 ||
      _addedProductFormatIds.Count > 0 ||
      _addedBrands.Count > 0 ||
      _addedMarkets.Count > 0;

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

      await AddMarketsAsync(markets, cancellationToken);
      await AddBrandsAsync(markets, cancellationToken);
      await AddProductsAndSnapshotsAsync(command, markets, cancellationToken);
    }

    private static void EnsureProductsAreUnique(IEnumerable<MarketImport> markets) {
      var identities = new HashSet<(string Market, string Name, string Brand, Quantity Quantity)>();
      foreach (MarketImport market in markets) {
        foreach (ProductImport product in market.Products) {
          ProductFormatImport? duplicate = product.Formats.FirstOrDefault(format =>
            !identities.Add((market.Name.Value.ToUpperInvariant(),
              product.Name.Value.ToUpperInvariant(), product.Brand.Value.ToUpperInvariant(),
              format.Quantity)));
          if (duplicate is not null) {
            throw new DuplicateMarketProductException(
              product.Name.Value, market.Name.Value, product.Brand.Value,
              (float)duplicate.Quantity.Amount, duplicate.Quantity.UnitOfMeasure.Value);
          }
        }
      }
    }

    private async Task AddMarketsAsync(
      IReadOnlyCollection<MarketImport> markets,
      CancellationToken cancellationToken
    ) {
      List<Market> existingMarkets = await marketRepository.GetMarketsAsync(
        cancellationToken);
      List<MarketName> newMarkets = [
        ..markets.Select(market => market.Name)
          .Where(name => !existingMarkets.Any(existing =>
            string.Equals(
              existing.Name.Value,
              name.Value,
              StringComparison.OrdinalIgnoreCase)))
      ];

      if (newMarkets.Count != 0) {
        await marketRepository.AddMarketsAsync(newMarkets, cancellationToken);
        _addedMarkets = newMarkets;
      }
    }

    private async Task AddBrandsAsync(
      IReadOnlyCollection<MarketImport> markets,
      CancellationToken cancellationToken
    ) {
      List<BrandName> brands = [
        ..markets.SelectMany(market => market.Products)
          .Select(product => product.Brand)
          .DistinctBy(brand => brand.Value, StringComparer.OrdinalIgnoreCase)
      ];

      IReadOnlyCollection<BrandName> existingBrands =
        await productRepository.GetBrandsAsync(cancellationToken);
      List<BrandName> newBrands = [
        ..brands.Where(brand => !existingBrands.Any(existing =>
          string.Equals(
            existing.Value,
            brand.Value,
            StringComparison.OrdinalIgnoreCase)))
      ];

      if (newBrands.Count != 0) {
        await productRepository.AddBrandsAsync(newBrands, cancellationToken);
        _addedBrands = newBrands;
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

      IMarketRepository marketRepo = services.GetRequiredService<IMarketRepository>();
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
      if (_addedBrands.Count > 0) {
        await productRepo.DeleteBrandsAsync(
          _addedBrands,
          cancellationToken);
      }
      if (_addedMarkets.Count > 0) {
        await marketRepo.DeleteMarketsAsync(
          _addedMarkets,
          cancellationToken);
      }
    }
  }
}