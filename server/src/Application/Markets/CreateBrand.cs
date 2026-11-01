using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Application.Markets;

public static class CreateBrand {
  public sealed record Command(string Name);

  public sealed class Handler(IBrandRepository repository) {
    public async Task<Guid> Handle(
      Command command, CancellationToken cancellationToken
    ) {
      ArgumentNullException.ThrowIfNull(command);

      var name = new BrandName(command.Name);
      if (await repository.ExistsAsync(name.Value, cancellationToken)) {
        throw new BrandAlreadyExistsException(name);
      }

      var brand = Brand.Create(command.Name);

      await repository.SaveAsync(brand, cancellationToken);

      return brand.Id.Value;
    }
  }
}