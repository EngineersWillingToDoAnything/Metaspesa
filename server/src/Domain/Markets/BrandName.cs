using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.Markets;

public readonly record struct BrandName {
  public string Value { get; }

  public BrandName(string value) {
    Value = value;
  }

  public static BrandName Create(string value) {
    if (string.IsNullOrWhiteSpace(value)) {
      throw new InvalidBrandNameException(value);
    }

    return new BrandName(value.Trim());
  }

  public bool Equals(BrandName other) =>
    StringComparer.InvariantCultureIgnoreCase.Equals(Value, other.Value);

  public override int GetHashCode() =>
    Value is null ? 0 : StringComparer.InvariantCultureIgnoreCase.GetHashCode(Value);

  public override string ToString() => Value;
}