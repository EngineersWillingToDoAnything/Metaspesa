using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.RestApi.Shopping.GetShoppingLists;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using static Metaspesa.Application.Shopping.GetShoppingListSummaries;
using static Metaspesa.RestApi.UnitTests.Shopping.ShoppingEndpointTestData;

namespace Metaspesa.RestApi.UnitTests.Shopping.GetShoppingLists;

public static class GetShoppingListsEndpointTests {
  [Fact]
  public static async Task List_ReturnsEmptyCollection_WhenShopperOwnsNoLists() {
    IShoppingListRepository repository = Substitute.For<IShoppingListRepository>();
    repository.GetByOwnerAsync(OwnerUid, TestContext.Current.CancellationToken)
      .Returns([]);

    IResult result = await GetShoppingListsEndpoint.ListAsync(
      ShopperContext(), new Handler(repository),
      TestContext.Current.CancellationToken);

    Assert.Empty(Assert.IsType<Ok<ShoppingListCollectionResponse>>(result)
      .Value!.Items);
  }

  [Fact]
  public static async Task List_ReturnsNamedAndTemporaryListIds() {
    // Arrange
    var namedList = new Response("Weekly", Guid.CreateVersion7(), false);
    var temporaryList = new Response(null, Guid.CreateVersion7(), true);
    IShoppingListRepository repository = Substitute.For<IShoppingListRepository>();
    repository.GetByOwnerAsync(OwnerUid,
      TestContext.Current.CancellationToken).Returns([
        namedList,
        temporaryList,
      ]);

    // Act
    IResult result = await GetShoppingListsEndpoint.ListAsync(
      ShopperContext(),
      new Handler(repository),
      TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal([
      new ShoppingListSummaryResponse(namedList.Id, "Weekly", false),
      new ShoppingListSummaryResponse(temporaryList.Id, null, true),
    ], Assert.IsType<Ok<ShoppingListCollectionResponse>>(result).Value!.Items);
  }

  [Fact]
  public static async Task List_RejectsRequestWithoutUserIdClaim() {
    IShoppingListRepository repository = Substitute.For<IShoppingListRepository>();

    Task action() => GetShoppingListsEndpoint.ListAsync(
      new DefaultHttpContext(),
      new Handler(repository),
      TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<UnauthorizedAccessException>(action);
  }
}