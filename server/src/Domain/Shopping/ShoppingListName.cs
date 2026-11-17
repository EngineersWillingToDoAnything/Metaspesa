using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.Shopping;

public readonly record struct ShoppingListName {
  public string Value { get; }

  public ShoppingListName(string value) {
    Value = value;
  }

  public static ShoppingListName Create(string value) {
    if (string.IsNullOrWhiteSpace(value)) {
      throw new InvalidShoppingListNameException();
    }

    return new ShoppingListName(value.Trim());
  }

  public override string ToString() => Value;
}