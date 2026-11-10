using Metaspesa.Domain.SharedKernel.Errors;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class PurchaseTests {
  [Fact]
  public void Create_PreservesIdentity() {
    // Arrange
    CreatePurchaseParams parameters = CreateParameters();

    // Act
    var purchase = Purchase.Create(parameters);

    // Assert
    Assert.Equal(parameters.PurchaseId, purchase.Id.Value);
  }

  [Fact]
  public void Create_AssignsBuyer() {
    // Arrange
    CreatePurchaseParams parameters = CreateParameters();
    var purchase = Purchase.Create(parameters);

    // Act & Assert
    Assert.True(purchase.WasMadeBy(parameters.BuyerId));
  }

  [Fact]
  public void Create_AssignsPurchaseTime() {
    // Arrange
    CreatePurchaseParams parameters = CreateParameters();
    var purchase = Purchase.Create(parameters);

    // Act & Assert
    Assert.True(purchase.WasMadeAt(parameters.PurchasedAt));
  }

  [Fact]
  public void Create_AssignsShoppingList() {
    // Arrange
    CreatePurchaseParams parameters = CreateParameters();
    var purchase = Purchase.Create(parameters);

    // Act & Assert
    Assert.True(purchase.WasForShoppingList(parameters.ShoppingListId));
  }

  [Fact]
  public void Create_AssignItems() {
    // Arrange
    CreatePurchaseParams parameters = CreateParameters();
    var purchase = Purchase.Create(parameters);

    // Act
    bool includesEveryItem = parameters.Items.All(item =>
      purchase.IncludesItem(item.PriceSnapshotId, item.Amount));

    // Assert
    Assert.True(includesEveryItem);
  }

  [Fact]
  public void Create_RejectsEmptyItems() {
    // Arrange
    CreatePurchaseParams parameters = CreateParameters() with { Items = [] };

    // Act
    Exception? failure = Record.Exception(() => Purchase.Create(parameters));

    // Assert
    Assert.IsType<EmptyCheckoutException>(failure);
  }

  [Fact]
  public void Create_RejectsDuplicateSnapshot() {
    // Arrange
    CreatePurchaseParams parameters = CreateParameters();
    Guid snapshotId = parameters.Items.First().PriceSnapshotId;
    parameters = parameters with { Items = [new(snapshotId, 1), new(snapshotId, 2)] };

    // Act
    Exception? failure = Record.Exception(() => Purchase.Create(parameters));

    // Assert
    Assert.IsType<DuplicatePurchaseItemException>(failure);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public void Create_RejectsNonPositiveAmounts(int amount) {
    // Arrange
    CreatePurchaseParams parameters = CreateParameters() with {
      Items = [new(Guid.CreateVersion7(), amount)],
    };

    // Act
    Exception? failure = Record.Exception(() => Purchase.Create(parameters));

    // Assert
    Assert.IsType<InvalidPositiveAmountException>(failure);
  }

  [Fact]
  public void Rehydrate_PreservesState() {
    // Arrange
    var originalPurchase = Purchase.Create(CreateParameters());

    // Act
    var purchase = Purchase.Rehydrate(originalPurchase.ToPrimitives());

    // Assert
    Assert.Equivalent(originalPurchase, purchase);
  }

  [Fact]
  public void Rehydrate_AcceptsDeletedBuyerReference() {
    // Arrange
    PurchasePrimitives persisted = CreatePersistedPurchase() with { BuyerId = null };
    var formerBuyerId = Guid.CreateVersion7();

    // Act
    var purchase = Purchase.Rehydrate(persisted);

    // Assert
    Assert.False(purchase.WasMadeBy(formerBuyerId));
  }

  [Fact]
  public void Rehydrate_AcceptsDeletedListReference() {
    // Arrange
    PurchasePrimitives persisted = CreatePersistedPurchase() with { ShoppingListId = null };
    var formerListId = Guid.CreateVersion7();

    // Act
    var purchase = Purchase.Rehydrate(persisted);

    // Assert
    Assert.False(purchase.WasForShoppingList(formerListId));
  }

  private static CreatePurchaseParams CreateParameters() => new(
    Guid.CreateVersion7(),
    Guid.CreateVersion7(),
    Guid.CreateVersion7(),
    DateTime.UtcNow,
    [
      new(Guid.CreateVersion7(), 2),
      new(Guid.CreateVersion7(), 3)
    ]);

  private static PurchasePrimitives CreatePersistedPurchase() => new(
    Guid.CreateVersion7(),
    Guid.CreateVersion7(),
    Guid.CreateVersion7(),
    DateTime.UtcNow,
    [
      new(Guid.CreateVersion7(), Guid.CreateVersion7(), 2),
      new(Guid.CreateVersion7(), Guid.CreateVersion7(), 3)
    ]);
}