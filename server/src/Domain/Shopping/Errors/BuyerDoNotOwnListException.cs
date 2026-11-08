namespace Metaspesa.Domain.Shopping.Errors;

public class BuyerDoNotOwnListException : ShoppingDomainException {
  private const string ErrorCode = "ShoppingList.Checkout.BuyerNotOwner";

  public BuyerDoNotOwnListException(
    ShoppingListId id, Guid buyerId
  ) : base(
    ErrorCode,
    $"Buyer '{buyerId}' does not own shopping list '{id}' and cannot check it out."
  ) { }

  public BuyerDoNotOwnListException()
    : base(ErrorCode, "Only an owner of the shopping list can check it out.") { }
  public BuyerDoNotOwnListException(string message) : base(ErrorCode, message) { }
  public BuyerDoNotOwnListException(
    string message, Exception innerException
  ) : base(ErrorCode, message, innerException) { }
}