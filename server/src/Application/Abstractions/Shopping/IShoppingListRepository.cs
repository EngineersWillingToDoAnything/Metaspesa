using Metaspesa.Application.Shopping;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.Application.Abstractions.Shopping;

public interface IShoppingListRepository {
  Task<IReadOnlyCollection<GetShoppingListSummaries.Response>> GetByOwnerAsync(
    Guid ownerId, CancellationToken cancellationToken);
  Task<ShoppingList?> GetAsync(
    Guid ownerId, Guid listId, CancellationToken cancellationToken);
  Task<bool> ExistsAsync(
    Guid ownerId, string? name, CancellationToken cancellationToken);
  Task<GetShoppingList.Response?> GetWithPricesAsync(
    Guid ownerId, Guid shoppingListId, CancellationToken cancellationToken);
  Task SaveAsync(ShoppingList shoppingList, CancellationToken cancellationToken);
}