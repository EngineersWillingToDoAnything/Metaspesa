using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.Markets;

public readonly record struct BrandId {
  public Guid Value { get; }

  public BrandId(Guid value) {
    if (value == Guid.Empty) {
      throw new InvalidBrandIdException(value);
    }
    Value = value;
  }

  public override string ToString() => Value.ToString();
}