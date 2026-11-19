using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.UnitTests.Markets;

public static class PriceSnapshotIdTests {
  [Fact(DisplayName = "Creates and compares price snapshots ids by value")]
  public static void PriceSnapshotId_CreatedAndEqual_WhenValueIsPositive() {
    var rawId = Guid.Parse("00000000-0000-7000-8000-000000000001");
    var id = PriceSnapshotId.Create(rawId);

    Assert.Equal(rawId, id.Value);
    Assert.Equal(id, PriceSnapshotId.Create(rawId));
  }

  [Fact(DisplayName = "Rejects empty price snapshot id")]
  public static void PriceSnapshotId_ThrowsSpecificException_WhenValueIsInvalid() =>
    Assert.Throws<InvalidPriceSnapshotIdException>(() =>
      PriceSnapshotId.Create(Guid.Empty));
}