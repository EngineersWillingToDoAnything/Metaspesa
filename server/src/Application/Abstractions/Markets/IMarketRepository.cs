using Metaspesa.Domain.Markets;

namespace Metaspesa.Application.Abstractions.Markets;

public interface IMarketRepository {
  Task<IReadOnlyDictionary<string, Guid>> ResolveMarketsAsync(
    IReadOnlyCollection<string> marketNames, CancellationToken cancellationToken);

  Task<bool> ExistsAsync(string marketName, CancellationToken cancellationToken);

  Task SaveAsync(Market market, CancellationToken cancellationToken);
  Task<IReadOnlyCollection<MarketSummary>> GetMarketSummariesAsync(
    CancellationToken cancellationToken);
}