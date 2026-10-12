using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class PurchaseTests {
  private static readonly DateTime PurchasedAt =
    new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

  [Fact(DisplayName = "Creates immutable purchase")]
  public void Create_ExposesReceiptState() {
    var buyerId = new UserId(Guid.CreateVersion7());
    var shoppingListId = new ShoppingListId(Guid.Parse("00000000-0000-7000-8000-000000000004"));
    var purchase = Purchase.Create(
      buyerId,
      shoppingListId,
      [Item(7, 2)],
      PurchasedAt);

    Assert.Equal(7, purchase.Id.Value.Version);
    Assert.Equal(buyerId, purchase.BuyerId);
    Assert.Equal(shoppingListId, purchase.ShoppingListId);
    Assert.Equal(PurchasedAt, purchase.PurchasedAt);
    Assert.Single(purchase.Items);
    Assert.All(
      typeof(Purchase).GetProperties(),
      property => Assert.False(property.CanWrite));
  }

  [Fact(DisplayName = "Rehydrates purchase with deleted references")]
  public void Rehydrate_AcceptsMissingBuyerAndList() {
    var purchase = Purchase.Rehydrate(
      new PurchaseId(Guid.Parse("00000000-0000-7000-8000-000000000005")), null, null, [Item(7, 2)], PurchasedAt);

    Assert.Equal(new PurchaseId(Guid.Parse("00000000-0000-7000-8000-000000000005")), purchase.Id);
    Assert.Null(purchase.BuyerId);
    Assert.Null(purchase.ShoppingListId);
  }

  [Fact(DisplayName = "Rejects empty purchase")]
  public void Create_ThrowsExactException_WhenItemsAreEmpty() {
    Assert.Throws<EmptyCheckoutException>(() =>
      Purchase.Create(null, null, [], PurchasedAt));
  }

  [Fact(DisplayName = "Rejects duplicate paid snapshot")]
  public void Create_ThrowsExactException_WhenSnapshotIsDuplicated() {
    Assert.Throws<DuplicatePurchaseItemException>(() =>
      Purchase.Create(null, null, [Item(7, 1), Item(7, 2)], PurchasedAt));
  }

  [Theory(DisplayName = "Rejects invalid purchase time")]
  [ClassData<InvalidPurchaseTimes>]
  public void Create_ThrowsExactException_WhenTimeIsInvalid(DateTime value) {
    Assert.Throws<InvalidPurchasedAtException>(() =>
      Purchase.Create(null, null, [Item(7, 1)], value));
  }

  private static PurchaseItem Item(int snapshotId, int amount) =>
    new(new PriceSnapshotId(Guid.Parse(
      $"00000000-0000-7000-8000-{snapshotId:x12}")),
      new PositiveAmount(amount));

  private sealed class InvalidPurchaseTimes() : TheoryData<DateTime>(
    default,
    new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Local),
    new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Unspecified)
  );
}