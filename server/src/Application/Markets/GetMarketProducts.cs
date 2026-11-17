using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Markets;

namespace Metaspesa.Application.Markets;

public static class GetMarketProducts {
  public record Query(GetMarketProductsFilter Filter);

  public class Handler(
    IProductRepository repository
  ) : IQueryHandler<Query, PagedResult<CatalogProduct>> {
    public async Task<PagedResult<CatalogProduct>> Handle(
      Query query, CancellationToken cancellationToken = default
    ) {
      ArgumentNullException.ThrowIfNull(query);
      return await repository.GetProductsAsync(query.Filter, cancellationToken);
    }
  }
}