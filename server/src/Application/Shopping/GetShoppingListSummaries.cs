using Metaspesa.Application.Abstractions.Shopping;

namespace Metaspesa.Application.Shopping;

public static class GetShoppingListSummaries {
  public record Query(Guid UserUid);
  public record Response(string? Name, Guid Id, bool IsTemporary);

  public class Handler(IShoppingListRepository shoppingListRepository) {
    public async Task<IReadOnlyCollection<Response>> Handle(
      Query query, CancellationToken cancellationToken = default
    ) {
      ArgumentNullException.ThrowIfNull(query);

      return await shoppingListRepository.GetByOwnerAsync(
        query.UserUid, cancellationToken);
    }
  }
}