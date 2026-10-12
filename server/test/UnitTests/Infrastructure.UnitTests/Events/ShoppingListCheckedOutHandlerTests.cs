using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Application.Shopping;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Events;
using Metaspesa.Infrastructure.Events;
using NSubstitute;

namespace Metaspesa.Infrastructure.UnitTests.Events;

public class ShoppingListCheckedOutHandlerTests {
  [Fact]
  public async Task HandleAsync_CreatesPurchaseFromCheckoutPayload() {
    var checkout = new ShoppingListCheckedOut(
      Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
      new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc),
      [new CheckedOutItem(Guid.CreateVersion7(), 2),
        new CheckedOutItem(Guid.CreateVersion7(), 3)]);
    var firstSnapshot = Guid.CreateVersion7();
    var secondSnapshot = Guid.CreateVersion7();
    Guid[] expectedSnapshots = [firstSnapshot, secondSnapshot];
    int[] expectedAmounts = [2, 3];
    IPurchasePriceSnapshotReader reader = Substitute.For<IPurchasePriceSnapshotReader>();
    IPurchaseRepository repository = Substitute.For<IPurchaseRepository>();
    reader.GetSnapshotsIdAsync(
      Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(
        checkout.Items.Select(item => item.ProductFormatId))),
      checkout.CheckoutAt, TestContext.Current.CancellationToken)
      .Returns(new Dictionary<Guid, Guid> {
        [checkout.Items.First().ProductFormatId] = firstSnapshot,
        [checkout.Items.Last().ProductFormatId] = secondSnapshot,
      });
    var handler = new ShoppingListCheckedOutHandler(new CreatePurchase.Handler(reader, repository));

    await handler.HandleAsync(checkout, TestContext.Current.CancellationToken);

    await repository.Received(1).SaveAsync(Arg.Is<Purchase>(purchase =>
      purchase.Id.Value == checkout.CheckoutId &&
      purchase.ShoppingListId!.Value.Value == checkout.ShoppingListId &&
      purchase.BuyerId!.Value.Value == checkout.BuyerId &&
      purchase.PurchasedAt == checkout.CheckoutAt &&
      purchase.Items.Select(item => item.PriceSnapshotId.Value)
        .SequenceEqual(expectedSnapshots) &&
      purchase.Items.Select(item => item.Amount.Value).SequenceEqual(expectedAmounts)),
      TestContext.Current.CancellationToken);
  }
}