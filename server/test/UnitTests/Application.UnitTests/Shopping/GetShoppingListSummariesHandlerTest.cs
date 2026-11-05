using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Shopping;
using NSubstitute;
using static Metaspesa.Application.Shopping.GetShoppingListSummaries;

namespace Metaspesa.Application.UnitTests.Shopping;

public class GetShoppingListSummariesHandlerTest {
  [Fact(DisplayName = "Returns application summary read models")]
  public async Task Handle_ReturnsReadModels() {
    var ownerId = Guid.CreateVersion7();
    IShoppingListRepository repository = Substitute.For<IShoppingListRepository>();
    repository.GetByOwnerAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
      .Returns([
        ShoppingList.Create(ownerId, "Weekly"),
        ShoppingList.Create(ownerId, null),
      ]);
    var handler = new Handler(repository);

    IReadOnlyCollection<Response> result = await handler.Handle(
      new Query(ownerId), TestContext.Current.CancellationToken);

    Assert.Collection(result,
      summary => Assert.Equal("Weekly", summary.Name),
      summary => Assert.Null(summary.Name));
  }

  [Fact(DisplayName = "Propagates cancellation")]
  public async Task Handle_PropagatesCancellation() {
    IShoppingListRepository repository = Substitute.For<IShoppingListRepository>();
    repository.GetByOwnerAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
      .Returns<Task<IReadOnlyCollection<ShoppingList>>>(_ =>
        throw new OperationCanceledException());
    var handler = new Handler(repository);

    await Assert.ThrowsAsync<OperationCanceledException>(() => handler.Handle(
      new Query(Guid.CreateVersion7()), TestContext.Current.CancellationToken));
  }
}