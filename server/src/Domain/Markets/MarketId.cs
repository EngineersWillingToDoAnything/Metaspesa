using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.Markets;

public readonly record struct MarketId {
  public Guid Value { get; }

  public MarketId(Guid value) {
    Value = value;
  }

  public static MarketId Create(Guid value) {
    if (value == Guid.Empty) {
      throw new InvalidMarketIdException(value);
    }

    return new MarketId(value);
  }

  public override string ToString() => Value.ToString();
}