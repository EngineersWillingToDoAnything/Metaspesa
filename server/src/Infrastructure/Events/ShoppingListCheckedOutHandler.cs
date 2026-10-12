using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Shopping;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping.Events;

namespace Metaspesa.Infrastructure.Events;

internal sealed class ShoppingListCheckedOutHandler(
  CreatePurchase.Handler handler
) : IDomainEventHandler {
  public bool CanHandle(IDomainEvent domainEvent) =>
    domainEvent is ShoppingListCheckedOut;

  public async Task HandleAsync(
    IDomainEvent domainEvent, CancellationToken cancellationToken = default
  ) {
    if (domainEvent is not ShoppingListCheckedOut checkout) {
      throw new ArgumentException("Unsupported domain event.", nameof(domainEvent));
    }

    var command = new CreatePurchase.Command(
      checkout.CheckoutId,
      checkout.ShoppingListId,
      checkout.BuyerId,
      checkout.CheckoutAt,
      [.. checkout.Items.Select(item =>
        new CreatePurchase.CommandItem(item.ProductFormatId, item.Amount))]);

    await handler.Handle(command, cancellationToken);
  }
}