using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;
using NSubstitute;
using static Metaspesa.Application.Shopping.UpdateItem;

namespace Metaspesa.Application.UnitTests.Shopping;

public class UpdateItemHandlerTest {
  private readonly IShoppingListRepository _shoppingListRepository;
  private readonly Handler _handler;

  public UpdateItemHandlerTest() {
    _shoppingListRepository = Substitute.For<IShoppingListRepository>();
    _handler = new Handler(_shoppingListRepository);
  }

  [Fact(DisplayName = "Rejects shopping list that does not exist")]
  public async Task Handle_RejectsShoppingListThatDoesNotExist() {
    var command = new Command(
      Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 2, true);
    _shoppingListRepository.GetAsync(
        command.UserUid, command.ShoppingListId,
        TestContext.Current.CancellationToken)
      .Returns((ShoppingList?)null);

    async Task action() => await _handler.Handle(
      command, TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<ShoppingListNotFoundException>(action);
  }

  [Fact(DisplayName = "Saves aggregate after updating item by ID")]
  public async Task Handle_SavesAggregateAfterUpdatingItemById() {
    var ownerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(ownerId, "Weekly");
    var item = new AddItemsParams(Guid.CreateVersion7(), 1, false);
    list.AddItems([item]);
    var command = new Command(
      ownerId, list.Id.Value, list.ItemIdFor(item), 4, true);
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
      ownerId, list.Id.Value, Guid.CreateVersion7(), 4, null);

    async Task action() => await _handler.Handle(
      command, TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<ShoppingItemNotFoundException>(action);
    await _shoppingListRepository.DidNotReceive().SaveAsync(
      Arg.Any<ShoppingList>(), Arg.Any<CancellationToken>());
  }
}