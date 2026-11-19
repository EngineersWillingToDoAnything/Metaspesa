using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.SharedKernel.Errors;

namespace Metaspesa.Domain.UnitTests.SharedKernel;

public class PositiveAmountTests {
  [Fact(DisplayName = "Creates positive amount")]
  public void Create_CreatesAmount_WhenValueIsPositive() {
    var amount = PositiveAmount.Create(2);

    Assert.Equal(2, amount.Value);
  }

  [Theory(DisplayName = "Rejects non-positive amount")]
  [InlineData(0)]
  [InlineData(-1)]
  public void Create_ThrowsExactException_WhenValueIsNotPositive(int value) {
    Assert.Throws<InvalidPositiveAmountException>(() => PositiveAmount.Create(value));
  }
}