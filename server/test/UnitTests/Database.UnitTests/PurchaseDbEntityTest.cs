using Metaspesa.Database.Entities;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.Database.UnitTests;

public static class PurchaseDbEntityTest {
  [Fact]
  public static void ToAggregate_PreservesPurchaseIdentity() {
    // Arrange
    var expected = Purchase.Create(new(
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      DateTime.UtcNow,
      [new CreatePurchaseItem(Guid.CreateVersion7(), 2)]
    ));
    PurchasePrimitives primitives = expected.ToPrimitives();
    var entity = new PurchaseDbEntity {
      Id = primitives.Id,
      UserUid = primitives.BuyerId,
      ShoppingListId = primitives.ShoppingListId,
      PurchasedAt = primitives.PurchasedAt,
      Items = [
        ..primitives.Items.Select(i => new PurchaseItemDbEntity {
          Id = i.Id,
          PriceSnapshotId = i.PriceSnapshotId,
          Amount = i.Amount,
        }),
      ],
    };

    // Act
    Purchase purchase = entity.ToAggregate();

    // Assert
    Assert.Equivalent(expected, purchase, strict: true);
  }
}