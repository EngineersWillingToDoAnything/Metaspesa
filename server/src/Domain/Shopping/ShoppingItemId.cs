using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.Shopping;

public readonly record struct ShoppingItemId {
  public Guid Value { get; }

  public ShoppingItemId(Guid value) {
    Value = value;
  }

  public static ShoppingItemId Create(Guid value) {
    if (value == Guid.Empty) {
      throw new InvalidShoppingItemIdException(value);
    }

    return new ShoppingItemId(value);
  }

  public override string ToString() => Value.ToString();
}