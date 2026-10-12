using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class PurchaseIdTests {
  [Fact(DisplayName = "Creates and compares purchase IDs by value")]
  public void Constructor_CreatesValueObject_WhenIdIsPositive() {
    Assert.Equal(new PurchaseId(Guid.Parse("00000000-0000-7000-8000-000000000001")), new PurchaseId(Guid.Parse("00000000-0000-7000-8000-000000000001")));
  }

  [Fact(DisplayName = "Rejects invalid purchase ID")]
  public void Constructor_ThrowsExactException_WhenIdIsInvalid() {
    Assert.Throws<InvalidPurchaseIdException>(() => new PurchaseId(Guid.Empty));
  }
}