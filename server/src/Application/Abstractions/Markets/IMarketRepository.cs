using Metaspesa.Domain.Markets;

namespace Metaspesa.Application.Abstractions.Markets;

public interface IMarketRepository {
  Task<IReadOnlyCollection<MarketSummary>> GetMarketSummariesAsync(
    CancellationToken cancellationToken);
  Task<List<Market>> GetMarketsAsync(CancellationToken cancellationToken);
  Task AddMarketsAsync(
    IReadOnlyCollection<MarketName> marketNames, CancellationToken cancellationToken);
  Task DeleteMarketsAsync(
    IReadOnlyCollection<MarketName> marketNames, CancellationToken cancellationToken);
}