using Metaspesa.Application.Abstractions.Shopping;
using NSubstitute;
using static Metaspesa.Application.Shopping.GetShoppingListSummaries;

namespace Metaspesa.Application.UnitTests.Shopping;

public class GetShoppingListSummariesHandlerTest {
  private readonly IShoppingListRepository _repository;

  private readonly Handler _handler;

  public GetShoppingListSummariesHandlerTest() {
    _repository = Substitute.For<IShoppingListRepository>();

    _handler = new Handler(_repository);
  }

  [Fact(DisplayName = "Returns application summary read models")]
  public async Task Handle_ReturnsReadModels() {
    // Arrange
    var ownerId = Guid.CreateVersion7();
    Response[] expected = [
      new("Weekly", Guid.CreateVersion7(), false),
      new(null, Guid.CreateVersion7(), true),
    ];
    _repository.GetByOwnerAsync(ownerId, TestContext.Current.CancellationToken)
      .Returns(expected);

    // Act
    IReadOnlyCollection<Response> result = await _handler.Handle(
      new Query(ownerId), TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(expected, result);
  }
}