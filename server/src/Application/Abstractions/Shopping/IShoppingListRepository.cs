using Metaspesa.Application.Shopping;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.Application.Abstractions.Shopping;

public interface IShoppingListRepository {
  Task<IReadOnlyCollection<ShoppingList>> GetByOwnerAsync(
    UserId ownerId, CancellationToken cancellationToken);
  Task<ShoppingList?> GetAsync(
    Guid ownerId, Guid listId, CancellationToken cancellationToken);
  Task<bool> ExistsAsync(
    Guid ownerId, string? name, CancellationToken cancellationToken);
  Task<Guid> AddAsync(ShoppingList shoppingList, CancellationToken cancellationToken);
  Task UpdateAsync(
    ShoppingList shoppingList, CancellationToken cancellationToken);
  Task<GetShoppingList.Response?> GetWithPricesAsync(
    Guid ownerId, Guid shoppingListId, CancellationToken cancellationToken);
  Task SaveAsync(ShoppingList shoppingList, CancellationToken cancellationToken);
}