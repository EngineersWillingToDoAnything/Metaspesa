using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Purchasing;
using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;
using Metaspesa.Domain.Shopping.Events;
using NSubstitute;
using static Metaspesa.Application.Shopping.CheckoutShoppingList;

namespace Metaspesa.Application.UnitTests.Shopping;

public class CheckoutShoppingListHandlerTests {
  private static readonly DateTime PurchasedAt =
    new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
  private static readonly Guid ListId = Guid.CreateVersion7();
  private static readonly Guid FirstFormatId = Guid.CreateVersion7();
  private static readonly Guid SecondFormatId = Guid.CreateVersion7();
  private static readonly Guid ThirdFormatId = Guid.CreateVersion7();
  private static readonly Guid FirstSnapshotId = Guid.CreateVersion7();
  private static readonly Guid SecondSnapshotId = Guid.CreateVersion7();
  private static readonly Guid PurchaseId = Guid.CreateVersion7();

  private readonly IShoppingListRepository _shoppingRepository =
    Substitute.For<IShoppingListRepository>();
  private readonly IPurchasePriceSnapshotReader _snapshotReader =
    Substitute.For<IPurchasePriceSnapshotReader>();
  private readonly IPurchaseRepository _purchaseRepository =
    Substitute.For<IPurchaseRepository>();
  private readonly IClock _clock = Substitute.For<IClock>();

  [Fact(DisplayName = "Creates purchase, resets list, and commits once")]
  public async Task Handle_ChecksOutNamedList_WhenCheckedItemsHaveSnapshots() {
    var ownerId = Guid.CreateVersion7();
    ShoppingList list = List(
      ownerId,
      "Weekly",
      Item(FirstFormatId, 3, true),
      Item(SecondFormatId, 1, false),
      Item(ThirdFormatId, 2, true));
    _shoppingRepository.GetAsync(
      new UserId(ownerId),
      new ShoppingListId(ListId),
      TestContext.Current.CancellationToken)
      .Returns(list);
    _snapshotReader.GetLatestAsync(
      Arg.Any<IReadOnlyCollection<ProductFormatId>>(),
      TestContext.Current.CancellationToken)
      .Returns(new Dictionary<ProductFormatId, PriceSnapshotId> {
        [new ProductFormatId(FirstFormatId)] = new PriceSnapshotId(FirstSnapshotId),
        [new ProductFormatId(ThirdFormatId)] = new PriceSnapshotId(SecondSnapshotId),
      });
    _clock.GetCurrentTime().Returns(PurchasedAt);
    _purchaseRepository.AddAsync(Arg.Any<Purchase>(),
      TestContext.Current.CancellationToken).Returns(PurchaseId);
    Handler handler = CreateHandler();

    Guid purchaseId = await handler.Handle(
      new Command(ownerId, ListId), TestContext.Current.CancellationToken);

    Assert.Equal(PurchaseId, purchaseId);
    await _purchaseRepository.Received(1).AddAsync(Arg.Is<Purchase>(purchase =>
      purchase.Id.Value.Version == 7 &&
      purchase.BuyerId == new UserId(ownerId) &&
      purchase.ShoppingListId == new ShoppingListId(ListId) &&
      purchase.PurchasedAt == PurchasedAt &&
      purchase.Items.Count == 2 &&
      purchase.Items.Any(item =>
        item.PriceSnapshotId == new PriceSnapshotId(FirstSnapshotId) &&
        item.Amount == new PositiveAmount(3)) &&
      purchase.Items.Any(item =>
        item.PriceSnapshotId == new PriceSnapshotId(SecondSnapshotId) &&
        item.Amount == new PositiveAmount(2))),
      TestContext.Current.CancellationToken);
    Assert.All(list.Items, item => Assert.False(item.IsChecked));
    await _shoppingRepository.Received(1).UpdateAsync(
      list, TestContext.Current.CancellationToken);
    ShoppingListCheckedOut domainEvent = Assert.IsType<ShoppingListCheckedOut>(
      Assert.Single(list.DomainEvents));
    Assert.Equal(PurchasedAt, domainEvent.CheckoutAt);
  }

  [Fact(DisplayName = "Loads temporary list by missing name")]
  public async Task Handle_ChecksOutTemporaryList_WhenNameIsMissing() {
    var ownerId = Guid.CreateVersion7();
    ShoppingList list = List(ownerId, null, Item(FirstFormatId, isChecked: true));
    _shoppingRepository.GetAsync(
      new UserId(ownerId), new ShoppingListId(ListId), TestContext.Current.CancellationToken)
      .Returns(list);
    _snapshotReader.GetLatestAsync(
      Arg.Any<IReadOnlyCollection<ProductFormatId>>(),
      TestContext.Current.CancellationToken)
      .Returns(new Dictionary<ProductFormatId, PriceSnapshotId> {
        [new ProductFormatId(FirstFormatId)] = new PriceSnapshotId(FirstSnapshotId),
      });
    _clock.GetCurrentTime().Returns(PurchasedAt);
    Handler handler = CreateHandler();

    await handler.Handle(
      new Command(ownerId, ListId), TestContext.Current.CancellationToken);

    await _shoppingRepository.Received(1).GetAsync(
      new UserId(ownerId), new ShoppingListId(ListId), TestContext.Current.CancellationToken);
    await _purchaseRepository.Received(1).AddAsync(
      Arg.Is<Purchase>(purchase => purchase.ShoppingListId == new ShoppingListId(ListId)),
      TestContext.Current.CancellationToken);
  }

  [Fact]
  public async Task Handle_ChecksOutOwnedListById() {
    var ownerId = Guid.CreateVersion7();
    ShoppingList list = List(ownerId, "Weekly", Item(FirstFormatId, isChecked: true));
    _shoppingRepository.GetAsync(new UserId(ownerId), new ShoppingListId(ListId),
      TestContext.Current.CancellationToken)
      .Returns(list);
    _snapshotReader.GetLatestAsync(
      Arg.Any<IReadOnlyCollection<ProductFormatId>>(),
      TestContext.Current.CancellationToken)
      .Returns(new Dictionary<ProductFormatId, PriceSnapshotId> {
        [new ProductFormatId(FirstFormatId)] = new PriceSnapshotId(FirstSnapshotId),
      });
    _clock.GetCurrentTime().Returns(PurchasedAt);
    _purchaseRepository.AddAsync(Arg.Any<Purchase>(),
      TestContext.Current.CancellationToken).Returns(PurchaseId);
    Handler handler = CreateHandler();

    Guid purchaseId = await handler.Handle(
      new Command(ownerId, ListId), TestContext.Current.CancellationToken);

    Assert.Equal(PurchaseId, purchaseId);
  }

  [Fact(DisplayName = "Rejects missing list without checkout work")]
  public async Task Handle_ThrowsShoppingException_WhenListIsMissing() {
    Handler handler = CreateHandler();

    await Assert.ThrowsAsync<ShoppingListNotFoundException>(() => handler.Handle(
      new Command(Guid.CreateVersion7(), Guid.CreateVersion7()),
      TestContext.Current.CancellationToken));

    await _snapshotReader.DidNotReceive().GetLatestAsync(
      Arg.Any<IReadOnlyCollection<ProductFormatId>>(),
      Arg.Any<CancellationToken>());
    await _purchaseRepository.DidNotReceive().AddAsync(
      Arg.Any<Purchase>(), Arg.Any<CancellationToken>());
  }

  [Fact(DisplayName = "Rejects list without checked items")]
  public async Task Handle_ThrowsPurchaseException_WhenNoItemIsChecked() {
    var ownerId = Guid.CreateVersion7();
    ShoppingList list = List(ownerId, "Weekly", Item(FirstFormatId, isChecked: false));
    _shoppingRepository.GetAsync(
      Arg.Any<UserId>(), Arg.Any<ShoppingListId>(), Arg.Any<CancellationToken>())
      .Returns(list);
    Handler handler = CreateHandler();

    await Assert.ThrowsAsync<EmptyPurchaseItemsException>(() => handler.Handle(
      new Command(ownerId, ListId), TestContext.Current.CancellationToken));

    await _snapshotReader.DidNotReceive().GetLatestAsync(
      Arg.Any<IReadOnlyCollection<ProductFormatId>>(),
      Arg.Any<CancellationToken>());
    await _purchaseRepository.DidNotReceive().AddAsync(
      Arg.Any<Purchase>(), Arg.Any<CancellationToken>());
    Assert.False(list.Items.Single().IsChecked);
    Assert.Empty(list.DomainEvents);
  }

  [Fact(DisplayName = "Rejects missing snapshot without resetting list")]
  public async Task Handle_ThrowsPurchaseException_WhenSnapshotIsMissing() {
    var ownerId = Guid.CreateVersion7();
    ShoppingList list = List(
      ownerId, "Weekly", Item(FirstFormatId, isChecked: true),
      Item(SecondFormatId, isChecked: true));
    _shoppingRepository.GetAsync(
      Arg.Any<UserId>(), Arg.Any<ShoppingListId>(), Arg.Any<CancellationToken>())
      .Returns(list);
    _snapshotReader.GetLatestAsync(
      Arg.Any<IReadOnlyCollection<ProductFormatId>>(),
      Arg.Any<CancellationToken>())
      .Returns(new Dictionary<ProductFormatId, PriceSnapshotId> {
        [new ProductFormatId(FirstFormatId)] = new PriceSnapshotId(FirstSnapshotId),
      });
    Handler handler = CreateHandler();

    await Assert.ThrowsAsync<PurchasePriceSnapshotNotFoundException>(() =>
      handler.Handle(
        new Command(ownerId, ListId),
        TestContext.Current.CancellationToken));

    await _purchaseRepository.DidNotReceive().AddAsync(
      Arg.Any<Purchase>(), Arg.Any<CancellationToken>());
    Assert.All(list.Items, item => Assert.True(item.IsChecked));
    Assert.Empty(list.DomainEvents);
    await _shoppingRepository.DidNotReceive().UpdateAsync(
      Arg.Any<ShoppingList>(), Arg.Any<CancellationToken>());
  }

  [Fact(DisplayName = "Propagates snapshot cancellation without mutation")]
  public async Task Handle_PropagatesCancellation_FromSnapshotReader() {
    var ownerId = Guid.CreateVersion7();
    ShoppingList list = List(ownerId, "Weekly", Item(FirstFormatId, isChecked: true));
    _shoppingRepository.GetAsync(
      Arg.Any<UserId>(), Arg.Any<ShoppingListId>(), Arg.Any<CancellationToken>())
      .Returns(list);
    _snapshotReader.GetLatestAsync(
      Arg.Any<IReadOnlyCollection<ProductFormatId>>(),
      Arg.Any<CancellationToken>())
      .Returns<Task<IReadOnlyDictionary<ProductFormatId, PriceSnapshotId>>>(_ =>
        throw new OperationCanceledException());
    Handler handler = CreateHandler();

    await Assert.ThrowsAsync<OperationCanceledException>(() => handler.Handle(
      new Command(ownerId, ListId), TestContext.Current.CancellationToken));

    await _purchaseRepository.DidNotReceive().AddAsync(
      Arg.Any<Purchase>(), Arg.Any<CancellationToken>());
    Assert.True(list.Items.Single().IsChecked);
    Assert.Empty(list.DomainEvents);
  }

  private Handler CreateHandler() => new(
    _shoppingRepository,
    _snapshotReader,
    _purchaseRepository,
    _clock);

  private static ShoppingList List(
    Guid ownerId,
    string? name,
    params ShoppingItem[] items
  ) => ShoppingList.Rehydrate(
    new ShoppingListId(ListId),
    [new UserId(ownerId)],
    name is null ? null : new ShoppingListName(name),
    null,
    items);

  private static ShoppingItem Item(
    Guid formatId, int amount = 1, bool isChecked = false
  ) => new(
    new ProductFormatId(formatId), new PositiveAmount(amount), isChecked);
}