using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Application.Shopping;

public static class CheckoutShoppingList {
  public record Command(Guid UserUid, Guid ShoppingListId);

  public class Handler(
    IShoppingListRepository shoppingListRepository,
    IClock clock
  ) {
    public async Task<Guid> Handle(
      Command command, CancellationToken cancellationToken = default
    ) {
      ArgumentNullException.ThrowIfNull(command);

      ShoppingList shoppingList = await shoppingListRepository.GetAsync(
        command.UserUid, command.ShoppingListId, cancellationToken)
        ?? throw new ShoppingListNotFoundException();

      Guid checkoutId = shoppingList.Checkout(
        command.UserUid, clock.GetCurrentTime());

      await shoppingListRepository.SaveAsync(shoppingList, cancellationToken);

      return checkoutId;
    }
  }
}