using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.Shopping;

public readonly record struct ShoppingListId {
  public Guid Value { get; }

  public ShoppingListId(Guid value) {
    Value = value;
  }

  public static ShoppingListId Create(Guid value) {
    if (value == Guid.Empty) {
      throw new InvalidShoppingListIdException(value);
    }

    return new ShoppingListId(value);
  }

  public override string ToString() => Value.ToString();
}