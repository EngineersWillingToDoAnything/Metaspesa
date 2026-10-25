using Metaspesa.Domain.SharedKernel.Errors;

namespace Metaspesa.Domain.SharedKernel;

public readonly record struct Quantity(
  decimal Amount,
  UnitOfMeasure UnitOfMeasure
) {
  public static Quantity Create(decimal amount, string unitOfMeasure) {
    if (amount <= 0) {
      throw new InvalidQuantityAmountException(amount);
    }

    var unit = UnitOfMeasure.Create(unitOfMeasure);
    return new Quantity(unit.ConvertToCanonValue(amount), unit.ToCanon());
  }
}