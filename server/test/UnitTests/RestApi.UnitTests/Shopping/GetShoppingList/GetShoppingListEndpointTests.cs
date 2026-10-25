using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.RestApi.Shopping.GetShoppingList;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using static Metaspesa.RestApi.UnitTests.Shopping.ShoppingEndpointTestData;
using GetList = Metaspesa.Application.Shopping.GetShoppingList;

namespace Metaspesa.RestApi.UnitTests.Shopping.GetShoppingList;

public static class GetShoppingListEndpointTests {
  [Fact(DisplayName = "Maps complete application response to API response")]
  public static async Task Get_MapsCompleteApplicationResponse() {
    IShoppingListRepository repository = Substitute.For<IShoppingListRepository>();
    repository.GetWithPricesAsync(
      OwnerUid, ListId, TestContext.Current.CancellationToken
    ).Returns(new GetList.Response(ListId, "Weekly", [
      new GetList.ResponseItem(
        FormatId,
        "Milk",
        "Brand",
        new MarketSummary(MarketUid, "Market", null),
        2,
        new GetList.ResponseItemFormat(
          Quantity.Create(1, "ml"),
          new Money(1.25m),
          new Uri("https://example.test/milk")),
        true),
    ]));

    IResult result = await GetShoppingListEndpoint.GetAsync(
      ListId,
      ShopperContext(),
      new GetList.Handler(repository),
      TestContext.Current.CancellationToken);

    var response = (Ok<ShoppingListResponse>)result;
    var expected = new ShoppingListResponse(
      ListId,
      "Weekly",
      false,
      [new ShoppingItemResponse(
        FormatId,
        "Milk",
        "Brand",
        new ShoppingMarketResponse(MarketUid, "Market"),
        new ShoppingQuantityResponse(1, "ml"),
        new ShoppingMoneyResponse(1.25m, "EUR"),
        2,
        true,
        "https://example.test/milk")]);
    Assert.Equivalent(expected, response.Value, strict: true);
  }

  [Fact(DisplayName = "Maps temporary empty list to API response")]
  public static async Task Get_MapsTemporaryEmptyList() {
    IShoppingListRepository repository = Substitute.For<IShoppingListRepository>();
    repository.GetWithPricesAsync(
      OwnerUid, ListId, TestContext.Current.CancellationToken
    ).Returns(new GetList.Response(ListId, null, []));

    IResult result = await GetShoppingListEndpoint.GetAsync(
      ListId,
      ShopperContext(),
      new GetList.Handler(repository),
      TestContext.Current.CancellationToken);

    var response = (Ok<ShoppingListResponse>)result;
    var expected = new ShoppingListResponse(ListId, null, true, []);
    Assert.Equivalent(expected, response.Value, strict: true);
  }

  [Fact(DisplayName = "Rejects request without authenticated user ID")]
  public static async Task Get_RejectsRequestWithoutUserIdClaim() {
    IShoppingListRepository repository = Substitute.For<IShoppingListRepository>();

    Task action() => GetShoppingListEndpoint.GetAsync(
        ListId,
        new DefaultHttpContext(),
        new GetList.Handler(repository),
        TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<UnauthorizedAccessException>(action);
  }
}