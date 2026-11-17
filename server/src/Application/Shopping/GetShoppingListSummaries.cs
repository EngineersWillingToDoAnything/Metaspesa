using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Shopping;

namespace Metaspesa.Application.Shopping;

public static class GetShoppingListSummaries {
  public record Query(Guid UserUid);
  public record Response(string? Name, Guid Id, bool IsTemporary);

  public class Handler(
    IShoppingListRepository repository
  ) : IQueryHandler<Query, IReadOnlyCollection<Response>> {
    public async Task<IReadOnlyCollection<Response>> Handle(
      Query query, CancellationToken cancellationToken = default
    ) {
      ArgumentNullException.ThrowIfNull(query);

      return await repository.GetByOwnerAsync(
        query.UserUid, cancellationToken);
    }
  }
}