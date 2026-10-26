using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Domain.Markets;

namespace Metaspesa.Application.Abstractions.Markets;

public interface IProductRepository {
  Task<Product?> GetByIdAsync(
    ProductId productId, CancellationToken cancellationToken);
  Task<PagedResult<CatalogProduct>> GetProductsAsync(
    GetMarketProductsFilter filter, CancellationToken cancellationToken);
  Task<ProductImportResult> ResolveProductsAsync(
    MarketImport market, DateTime observedAt, CancellationToken cancellationToken);
  Task DeleteProductsAsync(
    IReadOnlyCollection<ProductId> productIds, CancellationToken cancellationToken);
  Task DeleteProductFormatsAsync(
    IReadOnlyCollection<ProductFormatId> productFormatIds,
    CancellationToken cancellationToken);
  Task<bool> CheckFormatsExistAsync(
    IEnumerable<Guid> formatIds, CancellationToken cancellationToken);
}