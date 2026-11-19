using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Application.Shopping;

public static class UpdateShoppingList {
  public record Command(Guid UserUid, Guid ShoppingListId, string NewName);

  public class Handler(
    IShoppingListRepository repository
  ) : ICommandHandler<Command> {
    public async Task Handle(
      Command command, CancellationToken cancellationToken = default
    ) {
      ArgumentNullException.ThrowIfNull(command);

      bool alreadyExists = await repository.ExistsAsync(
        command.UserUid, command.NewName, cancellationToken);
      if (alreadyExists) {
        throw new ShoppingListAlreadyExistsException(
          command.UserUid, command.NewName);
      }

      ShoppingList shoppingList = await repository.GetAsync(
        command.UserUid, command.ShoppingListId, cancellationToken) ??
        throw new ShoppingListNotFoundException();

      shoppingList.Update(command.NewName);
      await repository.SaveAsync(shoppingList, cancellationToken);
    }
  }
}