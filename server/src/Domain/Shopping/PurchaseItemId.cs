
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.Shopping;

public readonly record struct PurchaseItemId {
  public Guid Value { get; }

  public PurchaseItemId(Guid value) {
    Value = value;
  }

  public static PurchaseItemId Create(Guid value) {
    if (value == Guid.Empty) {
      throw new InvalidPurchaseItemIdException(value);
    }

    return new PurchaseItemId(value);
  }

  public override string ToString() => Value.ToString();
}