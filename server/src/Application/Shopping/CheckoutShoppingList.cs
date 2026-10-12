using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Application.Shopping;

public static class CheckoutShoppingList {
  public record Command(Guid UserUid, Guid ShoppingListId);

  public class Handler(
    IShoppingListRepository repository,
    IClock clock,
    IDomainEventPublisher eventPublisher
  ) {
    public async Task<Guid> Handle(
      Command command, CancellationToken cancellationToken = default
    ) {
      ArgumentNullException.ThrowIfNull(command);

      ShoppingList shoppingList = await repository.GetAsync(
        command.UserUid, command.ShoppingListId, cancellationToken)
        ?? throw new ShoppingListNotFoundException();

      Guid checkoutId = shoppingList.Checkout(
        command.UserUid, clock.GetCurrentTime());

      await repository.SaveAsync(shoppingList, cancellationToken);
      await eventPublisher.PublishAsync(
        shoppingList.DomainEvents, CancellationToken.None);
      shoppingList.ClearDomainEvents();

      return checkoutId;
    }
  }
}