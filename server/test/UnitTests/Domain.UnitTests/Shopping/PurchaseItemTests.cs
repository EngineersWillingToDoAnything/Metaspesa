using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel.Errors;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class PurchaseItemTests {
  private static readonly Guid SnapshotId =
    Guid.Parse("00000000-0000-7000-8000-000000000003");

  [Fact]
  public void Create_PreservesSnapshotId() {
    var item = PurchaseItem.Create(SnapshotId, 2);

    Assert.Equal(SnapshotId, item.PriceSnapshotId.Value);
  }

  [Fact]
  public void Create_PreservesAmount() {
    var item = PurchaseItem.Create(SnapshotId, 2);

    Assert.Equal(2, item.Amount.Value);
  }

  [Fact]
  public void Create_GeneratesVersion7ItemId() {
    var item = PurchaseItem.Create(SnapshotId, 2);

    Assert.Equal(7, item.Id.Value.Version);
  }

  [Fact]
  public void Rehydrate_PreservesItemId() {
    var id = Guid.Parse("00000000-0000-7000-8000-000000000004");

    var item = PurchaseItem.Rehydrate(id, SnapshotId, 2);

    Assert.Equal(id, item.Id.Value);
  }

  [Fact]
  public void Rehydrate_RejectsEmptyItemId() {
    Assert.Throws<InvalidPurchaseItemIdException>(() =>
      PurchaseItem.Rehydrate(Guid.Empty, SnapshotId, 2));
  }

  [Fact]
  public void PurchaseItem_HasNoWritableProperties() {
    Assert.All(typeof(PurchaseItem).GetProperties(),
      property => Assert.False(property.CanWrite));
  }
}