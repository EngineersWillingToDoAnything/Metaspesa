using Metaspesa.Domain.SharedKernel.Errors;

namespace Metaspesa.Domain.SharedKernel;

public readonly record struct PositiveAmount {
  public int Value { get; }

  public PositiveAmount(int value) {
    Value = value;
  }

  public static PositiveAmount Create(int value) {
    if (value <= 0) {
      throw new InvalidPositiveAmountException(value);
    }

    return new PositiveAmount(value);
  }
}