using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;
using NSubstitute;
using static Metaspesa.Application.Shopping.RemoveItem;

namespace Metaspesa.Application.UnitTests.Shopping;

public class RemoveItemHandlerTest {
  private readonly IShoppingListRepository _shoppingListRepository;
  private readonly TimeProvider _clock;
  private readonly Handler _handler;

  public RemoveItemHandlerTest() {
    _shoppingListRepository = Substitute.For<IShoppingListRepository>();
    _clock = Substitute.For<TimeProvider>();
    _clock.GetUtcNow().Returns(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));

    _handler = new Handler(_shoppingListRepository, _clock);
  }

  [Fact(DisplayName = "Rejects shopping list that does not exist")]
  public async Task Handle_RejectsShoppingListThatDoesNotExist() {
    var command = new Command(
      Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
    _shoppingListRepository.GetAsync(
        command.UserUid, command.ShoppingListId,
        TestContext.Current.CancellationToken)
      .Returns((ShoppingList?)null);

    async Task action() => await _handler.Handle(
      command, TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<ShoppingListNotFoundException>(action);
  }

  [Fact(DisplayName = "Saves aggregate after removing item by ID")]
  public async Task Handle_SavesAggregateAfterRemovingItemById() {
    var ownerId = Guid.CreateVersion7();

    var list = ShoppingList.Create(ownerId, "Weekly");
    var item = new AddItemsParams(Guid.CreateVersion7(), 1, false);
    list.AddItems([item]);
    _clock.GetUtcNow().Returns(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));

    var command = new Command(ownerId, list.Id.Value, list.ItemIdFor(item));
    _shoppingListRepository.GetAsync(
        ownerId, list.Id.Value, TestContext.Current.CancellationToken)
      .Returns(list);

    await _handler.Handle(command, TestContext.Current.CancellationToken);

    await _shoppingListRepository.Received(1).SaveAsync(
      Arg.Is<ShoppingList>(value => value.Id == list.Id),
      TestContext.Current.CancellationToken);
  }

  [Fact(DisplayName = "Does not save when shopping item does not exist")]
  public async Task Handle_DoesNotSave_WhenShoppingItemDoesNotExist() {
    var ownerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(ownerId, "Weekly");
    _shoppingListRepository.GetAsync(
        ownerId, list.Id.Value, TestContext.Current.CancellationToken)
      .Returns(list);
    var command = new Command(
      ownerId, list.Id.Value, Guid.CreateVersion7());

    async Task action() => await _handler.Handle(
      command, TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<ShoppingItemNotFoundException>(action);
    await _shoppingListRepository.DidNotReceive().SaveAsync(
      Arg.Any<ShoppingList>(), Arg.Any<CancellationToken>());
  }
}