using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.Markets;

public readonly record struct MarketName {
  public string Value { get; }

  public MarketName(string value) {
    if (string.IsNullOrWhiteSpace(value)) {
      throw new InvalidMarketNameException(value);
    }

    Value = value.Trim();
  }

  public bool Equals(MarketName other) =>
    StringComparer.InvariantCultureIgnoreCase.Equals(Value, other.Value);

  public override int GetHashCode() =>
    Value is null ? 0 : StringComparer.InvariantCultureIgnoreCase.GetHashCode(Value);

  public override string ToString() => Value;
}