using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class PurchaseTests {
  [Fact]
  public void Create_PreservesExplicitId() {
    var purchaseId = Guid.CreateVersion7();

    var createParams = new CreatePurchaseParams(
      purchaseId,
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      DateTime.UtcNow,
      [new CreatePurchaseItem(Guid.CreateVersion7(), 2)]);

    var purchase = Purchase.Create(createParams);

    Assert.Equal(new PurchaseId(purchaseId), purchase.Id);
  }

  [Fact]
  public void Create_PreservesBuyer() {
    var buyerId = Guid.CreateVersion7();
    var createParams = new CreatePurchaseParams(
      Guid.CreateVersion7(),
      buyerId,
      Guid.CreateVersion7(),
      DateTime.UtcNow,
      [new CreatePurchaseItem(Guid.CreateVersion7(), 2)]);

    var purchase = Purchase.Create(createParams);

    Assert.Equal(new UserId(buyerId), purchase.BuyerId);
  }

  [Fact]
  public void Create_PreservesShoppingList() {
    var shoppingListId = Guid.CreateVersion7();

    var createParams = new CreatePurchaseParams(
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      shoppingListId,
      DateTime.UtcNow,
      [new CreatePurchaseItem(Guid.CreateVersion7(), 2)]);

    var purchase = Purchase.Create(createParams);

    Assert.Equal(new ShoppingListId(shoppingListId), purchase.ShoppingListId);
  }

  [Fact]
  public void Create_PreservesTimestamp() {
    DateTime purchasedAt = new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    var createParams = new CreatePurchaseParams(
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      purchasedAt,
      [new CreatePurchaseItem(Guid.CreateVersion7(), 2)]);

    var purchase = Purchase.Create(createParams);

    Assert.Equal(purchasedAt, purchase.PurchasedAt);
  }

  [Fact]
  public void Create_PreservesLineAmount() {
    const int purchaseItemAmount = 2;

    var createParams = new CreatePurchaseParams(
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      DateTime.UtcNow,
      [new CreatePurchaseItem(Guid.CreateVersion7(), purchaseItemAmount)]);

    var purchase = Purchase.Create(createParams);

    Assert.Equal(purchaseItemAmount, purchase.Items.Single().Amount.Value);
  }

  [Fact]
  public void Create_PreservesSnapshotId() {
    var snapshotId = Guid.CreateVersion7();
    var createParams = new CreatePurchaseParams(
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      DateTime.UtcNow,
      [new CreatePurchaseItem(snapshotId, 2)]);

    var purchase = Purchase.Create(createParams);

    Assert.Equal(snapshotId, purchase.Items.Single().PriceSnapshotId.Value);
  }

  [Fact]
  public void Create_RejectsEmptyItems() {
    var createParams = new CreatePurchaseParams(
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      DateTime.UtcNow,
      []);

    void action() => Purchase.Create(createParams);

    Assert.Throws<EmptyCheckoutException>(action);
  }

  [Fact]
  public void Create_RejectsDuplicateSnapshot() {
    var snapshotId = Guid.CreateVersion7();
    IReadOnlyCollection<CreatePurchaseItem> items = [
      new CreatePurchaseItem(snapshotId, 1),
      new CreatePurchaseItem(snapshotId, 2),
    ];
    var createParams = new CreatePurchaseParams(
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      DateTime.UtcNow,
      items);

    void action() => Purchase.Create(createParams);

    Assert.Throws<DuplicatePurchaseItemException>(action);
  }

  [Theory]
  [ClassData<InvalidPurchaseTimes>]
  public void Create_RejectsInvalidTime(DateTime value) {
    var createParams = new CreatePurchaseParams(
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      value,
      [new CreatePurchaseItem(Guid.CreateVersion7(), 2)]);

    void action() => Purchase.Create(createParams);

    Assert.Throws<InvalidPurchasedAtException>(action);
  }

  [Fact]
  public void Rehydrate_AcceptsDeletedBuyerReference() {
    var purchase = Purchase.Rehydrate(
      Guid.CreateVersion7(),
      null,
      Guid.CreateVersion7(),
      DateTime.UtcNow,
      [PurchaseItem.Create(Guid.CreateVersion7(), 2)]
    );

    Assert.Null(purchase.BuyerId);
  }

  [Fact]
  public void Rehydrate_AcceptsDeletedListReference() {
    var purchase = Purchase.Rehydrate(
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      null,
      DateTime.UtcNow,
      [PurchaseItem.Create(Guid.CreateVersion7(), 2)]);

    Assert.Null(purchase.ShoppingListId);
  }

  [Fact]
  public void Rehydrate_PreservesItemIdentity() {
    var item = PurchaseItem.Create(Guid.CreateVersion7(), 2);

    var purchase = Purchase.Rehydrate(
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      DateTime.UtcNow,
      [item]);

    Assert.Equal(item.Id, purchase.Items.Single().Id);
  }

  [Fact]
  public void Purchase_HasNoWritableProperties() {
    Assert.All(typeof(Purchase).GetProperties(),
      property => Assert.False(property.CanWrite));
  }

  private sealed class InvalidPurchaseTimes() : TheoryData<DateTime>(
    default,
    new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Local),
    new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Unspecified)
  );
}