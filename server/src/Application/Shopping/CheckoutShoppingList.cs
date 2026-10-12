using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Purchasing;
using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Markets;

using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Application.Shopping;

public static class CheckoutShoppingList {
  public record Command(Guid UserUid, Guid ShoppingListId);

  public class Handler(
    IShoppingListRepository shoppingListRepository,
    IPurchasePriceSnapshotReader priceSnapshotReader,
    IPurchaseRepository purchaseRepository,
    IClock clock
  ) {
    public async Task<Guid> Handle(
      Command command, CancellationToken cancellationToken = default
    ) {
      ArgumentNullException.ThrowIfNull(command);

      var ownerId = new UserId(command.UserUid);
      ShoppingList shoppingList = await shoppingListRepository.GetAsync(
        ownerId, new ShoppingListId(command.ShoppingListId),
        cancellationToken) ?? throw new ShoppingListNotFoundException();

      IReadOnlyCollection<ShoppingItem> checkedItems = shoppingList.CheckedItems();
      if (checkedItems.Count == 0) {
        throw new EmptyPurchaseItemsException();
      }

      List<PurchaseItem> purchaseItems = await CreatePurchaseItemsAsync(
        checkedItems, cancellationToken);

      ShoppingListId shoppingListId = shoppingList.Id;
      var purchase = Purchase.Create(
        ownerId,
        shoppingListId,
        purchaseItems,
        clock.GetCurrentTime());

      shoppingList.ResetCheckedItems();
      await shoppingListRepository.UpdateAsync(shoppingList, cancellationToken);
      return await purchaseRepository.AddAsync(purchase, cancellationToken);
    }

    private async Task<List<PurchaseItem>> CreatePurchaseItemsAsync(
      IReadOnlyCollection<ShoppingItem> checkedItems,
      CancellationToken cancellationToken
    ) {
      IReadOnlyCollection<ProductFormatId> productFormatIds = [
        .. checkedItems.Select(item => item.ProductFormatId).Distinct()
      ];

      IReadOnlyDictionary<ProductFormatId, PriceSnapshotId> snapshots =
        await priceSnapshotReader.GetLatestAsync(
          productFormatIds, cancellationToken);

      var purchaseItems = new List<PurchaseItem>(checkedItems.Count);
      foreach (ShoppingItem item in checkedItems) {
        if (!snapshots.TryGetValue(
          item.ProductFormatId, out PriceSnapshotId priceSnapshotId)) {
          throw new PurchasePriceSnapshotNotFoundException(item.ProductFormatId);
        }

        purchaseItems.Add(new PurchaseItem(priceSnapshotId, item.Amount));
      }

      return purchaseItems;
    }
  }
}