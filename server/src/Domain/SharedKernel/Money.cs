using Metaspesa.Domain.SharedKernel.Errors;

namespace Metaspesa.Domain.SharedKernel;

public readonly record struct Money(
  decimal Amount
) {
  private const int DecimalPlaces = 2;

  public static Money Create(decimal amount) {
    if (amount < 0) {
      throw new InvalidMoneyAmountException(amount);
    }

    return new Money(
      Math.Round(amount, DecimalPlaces, MidpointRounding.AwayFromZero));
  }
}