using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;
using NSubstitute;
using static Metaspesa.Application.Shopping.CreateShoppingList;

namespace Metaspesa.Application.UnitTests.Shopping;

public class CreateShoppingListHandlerTest {
  private readonly IShoppingListRepository _repository;
  private readonly Handler _handler;

  public CreateShoppingListHandlerTest() {
    _repository = Substitute.For<IShoppingListRepository>();

    _handler = new Handler(_repository);
  }

  [Fact]
  public async Task Handle_RejectsDuplicateNamedList() {
    var command = new Command(Guid.CreateVersion7(), "Weekly");
    _repository.ExistsAsync(command.UserUid,
        command.ShoppingListName, TestContext.Current.CancellationToken)
      .Returns(true);

    async Task action() => await _handler.Handle(
      command, TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<ShoppingListAlreadyExistsException>(action);
  }

  [Fact]
  public async Task Handle_SavesShoppingList_IfItDoesNotExist() {
    var command = new Command(Guid.CreateVersion7(), "Weekly");
    _repository.ExistsAsync(Arg.Any<Guid>(),
        Arg.Any<string?>(), TestContext.Current.CancellationToken)
      .Returns(false);

    await _handler.Handle(command, TestContext.Current.CancellationToken);

    await _repository.Received(1).SaveAsync(
      Arg.Is<ShoppingList>(list =>
        list.IsOwnedBy(command.UserUid) &&
        list.IsNamed(command.ShoppingListName)),
      TestContext.Current.CancellationToken);
  }

  [Fact]
  public async Task Handle_ReturnsIdFromNewList() {
    var command = new Command(Guid.CreateVersion7(), "Weekly");

    Guid id = await _handler.Handle(
      command, TestContext.Current.CancellationToken);

    await _repository.Received(1).SaveAsync(
      Arg.Is<ShoppingList>(list => list.Id.Value == id),
      TestContext.Current.CancellationToken);
  }
}