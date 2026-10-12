using Metaspesa.Domain.Shopping;

namespace Metaspesa.Application.Abstractions.Purchasing;

public interface IPurchaseRepository {
  Task<Guid> AddAsync(Purchase purchase, CancellationToken cancellationToken);
}