using Metaspesa.Domain.Shopping;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class PurchaseItemTests {
  [Fact]
  public void Rehydrate_RestoresRawValuesWithoutCreationValidation() {
    // Arrange
    var persisted = new PurchaseItemPrimitives(Guid.Empty, Guid.Empty, 0);

    // Act
    var item = PurchaseItem.Rehydrate(persisted);

    // Assert
    Assert.True(item.MatchesPurchase(persisted.PriceSnapshotId, persisted.Amount));
  }

  [Fact]
  public void Create_GeneratesDistinctPurchaseItemIdentities() {
    // Arrange
    var priceSnapshotId = Guid.CreateVersion7();
    var first = PurchaseItem.Create(priceSnapshotId, 2);

    // Act
    var second = PurchaseItem.Create(priceSnapshotId, 2);

    // Assert
    Assert.NotEqual(first.Id, second.Id);
  }

  [Fact]
  public void Rehydrate_PreservesState() {
    // Arrange
    var originalItem = PurchaseItem.Create(Guid.CreateVersion7(), 2);

    // Act
    var item = PurchaseItem.Rehydrate(originalItem.ToPrimitives());

    // Assert
    Assert.Equivalent(originalItem, item);
  }
}