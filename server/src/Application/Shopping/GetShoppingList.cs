using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Application.Shopping;

public static class GetShoppingList {
  public record ResponseItemFormat(
    Quantity Quantity, Money Price, Uri? ImageUrl
  );

  public record ResponseItem(
    Guid Id,
    string ProductName,
    string BrandName,
    MarketSummary Market,
    int Amount,
    ResponseItemFormat Format,
    bool IsChecked
  );

  public record Response(
    Guid Id, string? ShoppingListName, IReadOnlyCollection<ResponseItem> Items
  ) {
    public bool IsTemporary => ShoppingListName is null;
  };

  public record Query(Guid UserUid, Guid ShoppingListId);

  public class Handler(
    IShoppingListRepository repository
  ) : IQueryHandler<Query, Response> {
    public async Task<Response> Handle(
      Query query, CancellationToken cancellationToken = default
    ) {
      ArgumentNullException.ThrowIfNull(query);

      Response? shoppingListWithPrices = await repository.GetWithPricesAsync(
        query.UserUid, query.ShoppingListId, cancellationToken);

      return shoppingListWithPrices ??
        throw new ShoppingListNotFoundException();
    }
  }
}