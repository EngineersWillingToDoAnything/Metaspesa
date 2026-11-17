using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Application.Shopping;

public static class AddItemsToList {
  public record Command(
    Guid UserUid, Guid ShoppingListId, IReadOnlyCollection<AddItemsParams> Items
  );

  public class Handler(
    IShoppingListRepository shoppingListRepository,
    IProductRepository productRepository
  ) : ICommandHandler<Command> {
    public async Task Handle(
      Command command, CancellationToken cancellationToken = default
    ) {
      ArgumentNullException.ThrowIfNull(command);

      ShoppingList shoppingList = await shoppingListRepository.GetAsync(
        command.UserUid, command.ShoppingListId, cancellationToken) ??
        throw new ShoppingListNotFoundException();

      bool formatsExist = await productRepository.CheckFormatsExistAsync(
        command.Items.Select(item => item.ProductFormatUid),
        cancellationToken);

      if (!formatsExist) {
        throw new ShoppingProductFormatNotFoundException();
      }

      shoppingList.AddItems([.. command.Items]);

      await shoppingListRepository.SaveAsync(shoppingList, cancellationToken);
    }
  }
}