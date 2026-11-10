using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Application.Shopping;
using Metaspesa.Domain.Shopping;
using Metaspesa.RestApi.Shopping.RemoveShoppingItem;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using static Metaspesa.RestApi.UnitTests.Shopping.ShoppingEndpointTestData;

namespace Metaspesa.RestApi.UnitTests.Shopping.RemoveShoppingItem;

public class RemoveShoppingItemEndpointTests {
  private readonly IShoppingListRepository _repository;
  private readonly RemoveItem.Handler _handler;

  public RemoveShoppingItemEndpointTests() {
    _repository = Substitute.For<IShoppingListRepository>();

    _handler = new RemoveItem.Handler(
      _repository, Substitute.For<IClock>());
  }

  [Fact(DisplayName = "Returns no content for accepted request")]
  public async Task Remove_ReturnsNoContent_WhenRequestIsAccepted() {
    var list = ShoppingList.Create(OwnerUid, "Weekly");
    var item = new AddItemsParams(Guid.CreateVersion7(), 1, false);
    list.AddItems([item]);
    Guid itemId = list.ItemIdFor(item);

    _repository.GetAsync(
        OwnerUid, list.Id.Value, TestContext.Current.CancellationToken)
      .Returns(list);

    IResult result = await RemoveShoppingItemEndpoint.RemoveItemAsync(
      list.Id.Value, itemId, ShopperContext(), _handler,
      TestContext.Current.CancellationToken);

    IStatusCodeHttpResult statusCodeHttpResult = Assert.IsType<IStatusCodeHttpResult>(
      result, exactMatch: false);
    Assert.Equal(StatusCodes.Status204NoContent, statusCodeHttpResult.StatusCode);
  }

  [Fact(DisplayName = "Rejects request without authenticated user ID")]
  public async Task Remove_RejectsRequestWithoutUserIdClaim() {
    async Task action() => await RemoveShoppingItemEndpoint.RemoveItemAsync(
      ListId, Guid.CreateVersion7(), new DefaultHttpContext(), _handler,
      TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<UnauthorizedAccessException>(action);
  }
}