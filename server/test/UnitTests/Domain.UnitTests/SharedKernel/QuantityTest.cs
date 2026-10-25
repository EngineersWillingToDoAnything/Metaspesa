using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.SharedKernel.Errors;

namespace Metaspesa.Domain.UnitTests.SharedKernel;

public static class QuantityTest {
  public class Create {
    [Theory(DisplayName = "Throws specific exception when amount is non-positive")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Quantity_RejectsNonPositiveAmount(decimal amount) {
      void action() => Quantity.Create(amount, "kg");

      Assert.Throws<InvalidQuantityAmountException>(action);
    }

    [Fact(DisplayName = "Creates a Quantity with canonical values")]
    public void Quantity_Create_IsCanon() {
      var canonQuantity = Quantity.Create(1000, "g");

      var quantity = Quantity.Create(1, "kg");

      Assert.Equal(canonQuantity, quantity);
    }
  }
}