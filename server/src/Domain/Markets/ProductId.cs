using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.Markets;

public readonly record struct ProductId {
  public Guid Value { get; }

  public ProductId(Guid value) {
    Value = value;
  }

  public static ProductId Create(Guid value) {
    if (value == Guid.Empty) {
      throw new InvalidProductIdException(value);
    }

    return new ProductId(value);
  }

  public override string ToString() => Value.ToString();
}