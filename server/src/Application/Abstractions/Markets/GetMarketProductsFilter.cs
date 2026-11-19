using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Domain.Markets;

namespace Metaspesa.Application.Abstractions.Markets;

public sealed record GetMarketProductsFilter {
  public GetMarketProductsFilter(
    string? nameSegment,
    IReadOnlyCollection<MarketId> marketIds,
    string? brandNameSegment,
    Pagination pagination,
    CatalogSort sort = CatalogSort.Name
  ) {
    ArgumentNullException.ThrowIfNull(pagination);
    ArgumentNullException.ThrowIfNull(marketIds);
    MarketIds = [.. marketIds.Select(id => MarketId.Create(id.Value))];
    BrandNameSegment = brandNameSegment;
    NameSegment = nameSegment;
    Pagination = pagination;
    Sort = sort;
  }

  public IReadOnlyCollection<MarketId> MarketIds { get; }
  public string? BrandNameSegment { get; }
  public string? NameSegment { get; }
  public Pagination Pagination { get; }
  public CatalogSort Sort { get; }
}

public enum CatalogSort { Name, PriceAsc, PriceDesc }