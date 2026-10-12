using Metaspesa.Domain.Markets;

namespace Metaspesa.Application.Abstractions.Markets;

public interface IPriceSnapshotRepository {
  Task<IReadOnlyCollection<PriceSnapshot>> GetLatestForFormatsAsync(
    IReadOnlyCollection<ProductFormatId> productFormatIds,
    CancellationToken cancellationToken);
  Task AppendAsync(
    IReadOnlyCollection<PriceObservation> observations,
    CancellationToken cancellationToken);
  Task DeleteForMarketsAsync(
    IReadOnlyCollection<MarketName> marketNames,
    DateTime observedAt,
    CancellationToken cancellationToken);
}