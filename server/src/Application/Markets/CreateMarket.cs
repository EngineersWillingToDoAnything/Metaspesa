using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Application.Markets;

public static class CreateMarket {
  public sealed record Command(string Name, Uri? LogoUrl = null);

  public sealed class Handler(
    IMarketRepository repository
  ) : ICommandHandler<Command, Guid> {
    public async Task<Guid> Handle(
      Command command, CancellationToken cancellationToken = default
    ) {
      ArgumentNullException.ThrowIfNull(command);

      var name = new MarketName(command.Name);
      if (await repository.ExistsAsync(name.Value, cancellationToken)) {
        throw new MarketAlreadyExistsException(name);
      }

      var market = Market.Create(command.Name, command.LogoUrl);

      await repository.SaveAsync(market, cancellationToken);

      return market.Id.Value;
    }
  }
}