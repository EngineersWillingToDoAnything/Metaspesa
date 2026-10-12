using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class ShoppingDomainExceptionTests {
  [Theory(DisplayName = "Shopping exceptions derive from shared domain exception")]
  [ClassData<ShoppingExceptions>]
  public void ShoppingException_DerivesFromDomainException(Exception exception) {
    Assert.IsType<ShoppingDomainException>(exception, exactMatch: false);
  }

  private sealed class ShoppingExceptions() : TheoryData<Exception>(
    (Exception)new DuplicateShoppingItemException(),
    (Exception)new DuplicateShoppingListOwnerException(),
    (Exception)new EmptyCheckoutException(),
    (Exception)new EmptyShoppingItemsException(),
    (Exception)new EmptyShoppingItemUpdateException(),
    (Exception)new InvalidShoppingListIdException(),
    (Exception)new InvalidShoppingListNameException(),
    (Exception)new MissingShoppingListOwnerException(),
    (Exception)new ShoppingItemNotFoundException(),
    (Exception)new ShoppingListAlreadyExistsException(),
    (Exception)new ShoppingListNotFoundException(),
    (Exception)new ShoppingProductFormatNotFoundException()
  );
}