using Metaspesa.Domain.Shopping;

namespace Metaspesa.Application.Abstractions.Purchasing;

public interface IPurchaseRepository {
  Task<Purchase?> GetAsync(Guid purchaseId, CancellationToken cancellationToken);
  Task SaveAsync(Purchase purchase, CancellationToken cancellationToken);
}