using Metaspesa.Domain.Markets;

namespace Metaspesa.Application.Abstractions.Markets;

public interface IBrandRepository {
  Task<IReadOnlyDictionary<string, Guid>> ResolveBrandsAsync(
    IReadOnlyCollection<string> brandNames, CancellationToken cancellationToken);

  Task<bool> ExistsAsync(string brandName, CancellationToken cancellationToken);

  Task SaveAsync(Brand brand, CancellationToken cancellationToken);
}