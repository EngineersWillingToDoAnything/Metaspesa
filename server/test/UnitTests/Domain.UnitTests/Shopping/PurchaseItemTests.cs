using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class PurchaseItemTests {
  [Fact(DisplayName = "Creates immutable purchase item from typed values")]
  public void Constructor_ExposesTypedValues() {
    var item = new PurchaseItem(
      new PriceSnapshotId(Guid.Parse("00000000-0000-7000-8000-000000000003")), new PositiveAmount(2));

    Assert.Equal(new PriceSnapshotId(Guid.Parse("00000000-0000-7000-8000-000000000003")), item.PriceSnapshotId);
    Assert.Equal(new PositiveAmount(2), item.Amount);
    Assert.All(
      typeof(PurchaseItem).GetProperties(),
      property => Assert.False(property.CanWrite));
  }
}