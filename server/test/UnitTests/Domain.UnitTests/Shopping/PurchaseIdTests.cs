using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class PurchaseIdTests {
  [Fact(DisplayName = "Creates and compares purchase IDs by value")]
  public void Create_CreatesValueObject_WhenIdIsPositive() {
    Assert.Equal(PurchaseId.Create(Guid.Parse("00000000-0000-7000-8000-000000000001")), PurchaseId.Create(Guid.Parse("00000000-0000-7000-8000-000000000001")));
  }

  [Fact(DisplayName = "Rejects invalid purchase ID")]
  public void Create_ThrowsExactException_WhenIdIsInvalid() {
    Assert.Throws<InvalidPurchaseIdException>(() => PurchaseId.Create(Guid.Empty));
  }
}