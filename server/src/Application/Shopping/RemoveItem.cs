using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Application.Shopping;

public static class RemoveItem {
  public record Command(Guid UserUid, Guid ShoppingListId, Guid ShoppingItemId);

  public class Handler(
    IShoppingListRepository repository,
    TimeProvider timeProvider
  ) {
    public async Task Handle(
      Command command, CancellationToken cancellationToken = default
    ) {
      ArgumentNullException.ThrowIfNull(command);

      ShoppingList shoppingList = await repository.GetAsync(
        command.UserUid, command.ShoppingListId, cancellationToken) ??
        throw new ShoppingListNotFoundException();

      DateTime now = timeProvider.GetUtcNow().UtcDateTime;

      shoppingList.RemoveItem(command.ShoppingItemId, now);

      await repository.SaveAsync(shoppingList, cancellationToken);
    }
  }
}