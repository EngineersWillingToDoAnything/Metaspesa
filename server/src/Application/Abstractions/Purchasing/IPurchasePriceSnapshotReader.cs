namespace Metaspesa.Application.Abstractions.Purchasing;

public interface IPurchasePriceSnapshotReader {
  Task<IReadOnlyDictionary<Guid, Guid>> GetSnapshotsIdAsync(
    IReadOnlyCollection<Guid> productFormatIds,
    DateTime purchasedAt,
    CancellationToken cancellationToken);
}