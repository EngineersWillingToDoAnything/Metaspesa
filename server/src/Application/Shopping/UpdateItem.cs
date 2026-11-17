using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Application.Shopping;

public static class UpdateItem {
  public record Command(
    Guid UserUid,
    Guid ShoppingListId,
    Guid ShoppingItemId,
    int? Amount,
    bool? IsChecked
  );

  public class Handler(
    IShoppingListRepository repository
  ) : ICommandHandler<Command> {
    public async Task Handle(
      Command command, CancellationToken cancellationToken = default
    ) {
      ArgumentNullException.ThrowIfNull(command);

      ShoppingList shoppingList = await repository.GetAsync(
        command.UserUid, command.ShoppingListId, cancellationToken) ??
        throw new ShoppingListNotFoundException();

      shoppingList.UpdateItem(
        command.ShoppingItemId, command.Amount, command.IsChecked);
      await repository.SaveAsync(shoppingList, cancellationToken);
    }
  }
}