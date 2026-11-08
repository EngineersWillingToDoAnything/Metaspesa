using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Application.Shopping;
using Metaspesa.Domain.Shopping;
using Metaspesa.RestApi.Shopping.UpdateShoppingItem;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using static Metaspesa.RestApi.UnitTests.Shopping.ShoppingEndpointTestData;

namespace Metaspesa.RestApi.UnitTests.Shopping.UpdateShoppingItem;

public class UpdateShoppingItemEndpointTests {
  private readonly IShoppingListRepository _shoppingListRepository;
  private readonly UpdateItem.Handler _handler;

  public UpdateShoppingItemEndpointTests() {
    _shoppingListRepository = Substitute.For<IShoppingListRepository>();
    _handler = new UpdateItem.Handler(_shoppingListRepository);
  }

  [Fact(DisplayName = "Returns no content for accepted request")]
  public async Task Update_ReturnsNoContent_WhenRequestIsAccepted() {
    // Arrange
    var list = ShoppingList.Create(OwnerUid, "Weekly");
    var item = new AddItemsParams(Guid.CreateVersion7(), 1, false);
    list.AddItems([item]);
    Guid itemId = list.ItemIdFor(item);
    _shoppingListRepository.GetAsync(
        OwnerUid, list.Id.Value, TestContext.Current.CancellationToken)
      .Returns(list);
    var request = new UpdateShoppingItemRequest(3, true);

    // Act
    IResult result = await UpdateShoppingItemEndpoint.UpdateItemAsync(
      list.Id.Value, itemId, request, ShopperContext(), _handler,
      TestContext.Current.CancellationToken);

    // Assert
    IStatusCodeHttpResult statusCodeHttpResult = Assert.IsType<IStatusCodeHttpResult>(
      result, exactMatch: false);
    Assert.Equal(StatusCodes.Status204NoContent, statusCodeHttpResult.StatusCode);
  }

  [Fact(DisplayName = "Rejects request without authenticated user ID")]
  public async Task Update_RejectsRequestWithoutUserIdClaim() {
    var request = new UpdateShoppingItemRequest(3, true);

    async Task action() => await UpdateShoppingItemEndpoint.UpdateItemAsync(
      ListId, Guid.CreateVersion7(), request, new DefaultHttpContext(), _handler,
      TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<UnauthorizedAccessException>(action);
  }
}