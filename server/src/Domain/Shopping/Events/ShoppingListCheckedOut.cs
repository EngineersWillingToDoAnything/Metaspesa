using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Shopping.Events;

public record ShoppingListCheckedOut : IDomainEvent {
  public Guid CheckoutId { get; }
  public Guid ShoppingListId { get; }
  public Guid BuyerId { get; }
  public DateTime CheckoutAt { get; }
  public IReadOnlyCollection<CheckedOutItem> Items { get; }

  public ShoppingListCheckedOut(
    Guid checkoutId,
    Guid shoppingListId,
    Guid buyerId,
    DateTime checkoutAt,
    IReadOnlyCollection<CheckedOutItem> items
  ) {
    ArgumentNullException.ThrowIfNull(items);

    CheckoutId = checkoutId;
    ShoppingListId = shoppingListId;
    BuyerId = buyerId;
    CheckoutAt = checkoutAt;
    Items = Array.AsReadOnly(items.ToArray());
  }
}

public sealed record CheckedOutItem(
  Guid ProductFormatId,
  int Amount
);