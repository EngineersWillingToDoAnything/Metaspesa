using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.SharedKernel.Errors;

namespace Metaspesa.Domain.UnitTests.SharedKernel;

public class MoneyTest {
  [Theory]
  [InlineData(0)]
  [InlineData(0.01)]
  [InlineData(12.34)]
  public void Create_PreservesNonNegativeAmount(decimal amount) {
    var money = Money.Create(amount);

    Assert.Equal(amount, money.Amount);
  }

  [Theory]
  [InlineData(-0.01)]
  [InlineData(-1)]
  public void Create_RejectsNegativeAmount(decimal amount) {
    void action() => Money.Create(amount);

    Assert.Throws<InvalidMoneyAmountException>(action);
  }

  [Fact]
  public void Create_ReportsRejectedAmount() {
    Exception? error = Record.Exception(() => Money.Create(-0.01m));

    Assert.Equal(-0.01m, (error as InvalidMoneyAmountException)?.Amount);
  }

  [Theory]
  [InlineData(12.345, 12.35)]
  [InlineData(12.344, 12.34)]
  [InlineData(0.005, 0.01)]
  [InlineData(0.004, 0)]
  public void Create_RoundsToTwoDecimalPlaces(decimal amount, decimal expected) {
    var money = Money.Create(amount);

    Assert.Equal(expected, money.Amount);
  }

  [Theory]
  [InlineData(-0.01)]
  [InlineData(3.451)]
  [InlineData(0)]
  public void Constructor_PreservesRawAmount(decimal amount) {
    var money = new Money(amount);

    Assert.Equal(amount, money.Amount);
  }

  [Fact]
  public void Equality_ComparesAmountValues() {
    var first = new Money(5);
    var second = new Money(5);

    Assert.Equal(first, second);
  }

  [Fact]
  public void Equality_DistinguishesDifferentAmounts() {
    var first = new Money(5);
    var second = new Money(6);

    Assert.NotEqual(first, second);
  }
}