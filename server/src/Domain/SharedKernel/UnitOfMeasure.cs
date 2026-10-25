using Metaspesa.Domain.SharedKernel.Errors;

namespace Metaspesa.Domain.SharedKernel;

public readonly record struct UnitOfMeasure(
  string Value
) {
  private static readonly string[] SupportedUnits = [
    "unit",
    "wash",
    "dose",
    "kg",
    "g",
    "mg",
    "l",
    "ml",
    "cl",
  ];

  private string CanonCode => Value switch {
    "kg" => "g",
    "mg" => "g",
    "l" => "ml",
    "cl" => "ml",
    _ => Value,
  };

  private decimal CanonFactor => Value switch {
    "kg" => 1000m,
    "mg" => 0.001m,
    "l" => 1000m,
    "cl" => 10m,
    _ => 1m,
  };

  public static UnitOfMeasure Create(string value) {
    string normalizedValue = FindSupportedUnit(value) ??
      throw InvalidUnitOfMeasureException.ForUnit(value);

    return new UnitOfMeasure(normalizedValue);
  }

  public decimal ConvertToCanonValue(decimal amount) {
    if (amount < 0) {
      throw new InvalidQuantityAmountException(amount);
    }

    return amount * CanonFactor;
  }

  public UnitOfMeasure ToCanon() => Create(CanonCode);

  private static string? FindSupportedUnit(string? value) {
    string? trimmedValue = value?.Trim();
    return SupportedUnits.FirstOrDefault(
      unit => string.Equals(unit, trimmedValue, StringComparison.OrdinalIgnoreCase));
  }

  public override string ToString() => Value;
}