using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

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
  );

  public sealed class Handler(
    IMarketRepository marketRepository,
    IBrandRepository brandRepository,
    IProductRepository productRepository
  ) {
    private const int BatchSize = 1_000;

    private sealed record NamedProduct(
      CommandProduct Source,
      MarketName Market,
      BrandName Brand,
      ProductName Name);

    private sealed record ObservationGroup(
      ProductIdentity Identity, ProductObservationParams Observation);

    public async Task Handle(
      Command command, CancellationToken cancellationToken = default
    ) {
      ArgumentNullException.ThrowIfNull(command);
      ArgumentNullException.ThrowIfNull(command.Products);

      if (command.Products.Count == 0) {
        throw new EmptyMarketProductsException();
      }
      if (command.RegisteredAt < new DateOnly(2023, 1, 1)) {
        throw new InvalidMarketProductsRegisteredAtException(command.RegisteredAt);
      }

      NamedProduct[] products = MapNamedProducts(command);

      Dictionary<MarketName, Guid> markets = await ResolveMarketsAsync(
        products, cancellationToken);
      Dictionary<BrandName, Guid> brands = await ResolveBrandsAsync(
        products, cancellationToken);

      ObservationGroup[] groups = GroupObservations(
        products, markets, brands, command.RegisteredAt);

      foreach (ObservationGroup[] batch in groups.Chunk(BatchSize)) {
        await ProcessBatchAsync(batch, cancellationToken);
      }
    }

    private static NamedProduct[] MapNamedProducts(Command command) {
      return [.. command.Products.Select(product => new NamedProduct(
        product,
        new MarketName(product.MarketName!),
        new BrandName(product.BrandName!),
        ProductName.Create(product.Name!)))
      ];
    }

    private async Task ProcessBatchAsync(
      ObservationGroup[] observations, CancellationToken cancellationToken
    ) {
      IReadOnlyDictionary<ProductIdentity, Product> existingBatch = await productRepository
        .GetBatchAsync([.. observations.Select(group => group.Identity)], cancellationToken);
      var batch = new List<Product>(observations.Length);

      foreach (ObservationGroup group in observations) {
        if (existingBatch.TryGetValue(group.Identity, out Product? product)) {
          product.RegisterObservation(group.Observation);
        } else {
          product = Product.Create(group.Observation);
        }
        batch.Add(product);
      }

      await productRepository.SaveBatchAsync(batch, cancellationToken);
    }

    private static ObservationGroup[] GroupObservations(
      NamedProduct[] products,
      Dictionary<MarketName, Guid> markets,
      Dictionary<BrandName, Guid> brands,
      DateOnly observedOn
    ) {
      return [..products
        .GroupBy(product => new ProductIdentity(
          new MarketId(markets[product.Market]),
          new BrandId(brands[product.Brand]),
          product.Name))
        .Select(group => new ObservationGroup(
          group.Key,
          new ProductObservationParams(
            group.Key.MarketId.Value,
            group.Key.BrandId.Value,
            group.Key.Name.Value,
            observedOn,
            [.. group.Select(product => new ProductFormatObservationParams(
              product.Source.Quantity,
              UnitOfMeasureNormalizer.Normalize(product.Source.UnitOfMeasure)!,
              product.Source.Price,
              product.Source.ImageUrl!))
            ]
        )))
      ];
    }

    private async Task<Dictionary<MarketName, Guid>> ResolveMarketsAsync(
      IReadOnlyCollection<NamedProduct> products, CancellationToken cancellationToken
    ) {
      IReadOnlyDictionary<string, Guid> resolvedMarkets = await marketRepository.ResolveMarketsAsync(
        [.. products.Select(product => product.Market.Value).Distinct(StringComparer.InvariantCultureIgnoreCase)],
        cancellationToken);
      var markets = resolvedMarkets.ToDictionary(
        pair => new MarketName(pair.Key), pair => pair.Value);

      NamedProduct? unknownMarket = products.FirstOrDefault(product =>
        !markets.ContainsKey(product.Market));
      if (unknownMarket is not null) {
        throw new UnknownMarketException(unknownMarket.Market);
      }

      return markets;
    }

    private async Task<Dictionary<BrandName, Guid>> ResolveBrandsAsync(
      IReadOnlyCollection<NamedProduct> products, CancellationToken cancellationToken
    ) {
      IReadOnlyDictionary<string, Guid> resolvedBrands = await brandRepository.ResolveBrandsAsync(
        [.. products.Select(product => product.Brand.Value).Distinct(StringComparer.InvariantCultureIgnoreCase)],
        cancellationToken);
      var brands = resolvedBrands.ToDictionary(
        pair => new BrandName(pair.Key), pair => pair.Value);

      NamedProduct? unknownBrand = products.FirstOrDefault(product =>
        !brands.ContainsKey(product.Brand));
      if (unknownBrand is not null) {
        throw new UnknownProductBrandException(
          unknownBrand.Market, unknownBrand.Name, unknownBrand.Brand);
      }

      return brands;
    }
  }
}