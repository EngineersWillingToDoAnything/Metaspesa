using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.Markets;

public readonly record struct ProductName(
  string Value
) {
  public static ProductName Create(string value) {
    if (string.IsNullOrWhiteSpace(value)) {
      throw new InvalidProductNameException(value);
    }

    return new ProductName(value.Trim());
  }

  public bool Equals(ProductName other) =>
    StringComparer.InvariantCultureIgnoreCase.Equals(Value, other.Value);

  public override int GetHashCode() =>
    Value is null ? 0 : StringComparer.InvariantCultureIgnoreCase.GetHashCode(Value);

  public override string ToString() => Value;
}