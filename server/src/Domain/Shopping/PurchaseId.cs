

using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.Shopping;

public readonly record struct PurchaseId {
  public Guid Value { get; }

  public PurchaseId(Guid value) {
    if (value == Guid.Empty) {
      throw new InvalidPurchaseIdException(value);
    }

    Value = value;
  }

  public override string ToString() => Value.ToString();
}