using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Markets;

namespace Metaspesa.Application.Markets;

public static class GetMarkets {
  public class Handler(
    IMarketRepository marketRepository
  ) : IQueryHandler<IReadOnlyCollection<MarketSummary>> {
    public async Task<IReadOnlyCollection<MarketSummary>> Handle(
      CancellationToken cancellationToken = default
    ) {
      return await marketRepository.GetMarketSummariesAsync(cancellationToken);
    }
  }
}