
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.Shopping;

public readonly record struct PurchaseItemId {
  public Guid Value { get; }

  public PurchaseItemId(Guid value) {
    if (value == Guid.Empty) {
      throw new InvalidPurchaseItemIdException(value);
    }

    Value = value;
  }

  public override string ToString() => Value.ToString();
}