using Metaspesa.Domain.Markets;

namespace Metaspesa.Domain.Shopping.Errors;

public class DuplicateShoppingItemException : ShoppingDomainException {
  public DuplicateShoppingItemException() { }
  public DuplicateShoppingItemException(Guid productFormatId)
    : base(
      "ShoppingList.Item.DuplicateReferenceUid",
      $"Product format '{productFormatId}' already belongs to this shopping list.") { }
  public DuplicateShoppingItemException(string message) : base(message) { }
  public DuplicateShoppingItemException(string message, Exception innerException)
    : base(message, innerException) { }
}