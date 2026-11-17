using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.Markets;

public readonly record struct ProductFormatId {
  public Guid Value { get; }

  public ProductFormatId(Guid value) {
    Value = value;
  }

  public static ProductFormatId Create(Guid value) {
    if (value == Guid.Empty) {
      throw new InvalidProductFormatIdException(value);
    }

    return new ProductFormatId(value);
  }

  public override string ToString() => Value.ToString();
}