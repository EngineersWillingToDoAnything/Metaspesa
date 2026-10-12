using Metaspesa.Application.Abstractions.Purchasing;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Application.Shopping;

public static class CreatePurchase {
  public record Command(
    Guid PurchaseId,
    Guid ShoppingListId,
    Guid BuyerId,
    DateTime PurchasedAt,
    IReadOnlyCollection<CommandItem> Items);

  public record CommandItem(Guid ProductFormatId, int Amount);

  public class Handler(
    IPurchasePriceSnapshotReader priceSnapshotReader,
    IPurchaseRepository purchaseRepository
  ) {
    public async Task<Guid> Handle(
      Command command, CancellationToken cancellationToken = default
    ) {
      ArgumentNullException.ThrowIfNull(command);

      Purchase? existingPurchase = await purchaseRepository.GetAsync(
        command.PurchaseId, cancellationToken);

      if (existingPurchase is not null) {
        return existingPurchase.Id.Value;
      }

      List<CreatePurchaseItem> purchaseItems = await CreatePurchaseItemsAsync(
        command, cancellationToken);

      var createParams = new CreatePurchaseParams(
        command.PurchaseId,
        command.BuyerId,
        command.ShoppingListId,
        command.PurchasedAt,
        purchaseItems
      );

      var purchase = Purchase.Create(createParams);

      await purchaseRepository.SaveAsync(purchase, cancellationToken);

      return purchase.Id.Value;
    }

    private async Task<List<CreatePurchaseItem>> CreatePurchaseItemsAsync(
      Command command,
      CancellationToken cancellationToken
    ) {
      CommandItem[] checkedItems = [.. command.Items];
      IReadOnlyCollection<Guid> productFormatIds = [
        .. checkedItems.Select(item => item.ProductFormatId).Distinct()
      ];

      IReadOnlyDictionary<Guid, Guid> snapshots =
        await priceSnapshotReader.GetSnapshotsIdAsync(
          productFormatIds, command.PurchasedAt, cancellationToken);

      var purchaseItems = new List<CreatePurchaseItem>(checkedItems.Length);
      foreach (CommandItem item in checkedItems) {
        if (!snapshots.TryGetValue(item.ProductFormatId, out Guid priceSnapshotId)) {
          throw new PurchasePriceSnapshotNotFoundException(item.ProductFormatId);
        }

        purchaseItems.Add(new CreatePurchaseItem(priceSnapshotId, item.Amount));
      }

      return purchaseItems;
    }
  }
}