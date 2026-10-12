using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Events;
using Metaspesa.RestApi.Shopping.CheckoutShoppingList;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using CheckoutUseCase = Metaspesa.Application.Shopping.CheckoutShoppingList;

namespace Metaspesa.RestApi.UnitTests.Shopping.CheckoutShoppingList;

public class CheckoutShoppingListEndpointTests {
  private readonly FakeHandler handler;

  private sealed class FakeHandler : CheckoutUseCase.Handler {
    private readonly IShoppingListRepository _repository;
    private readonly IClock _clock;
    private readonly ShoppingList _list;

    public FakeHandler(
      IShoppingListRepository repository, IClock clock
    ) : base(repository, clock) {
      _repository = repository;
      _clock = clock;
      _list = ShoppingList.Create(Guid.CreateVersion7(), "Weekly");
    }

    public void WithHappyPath() {
      _list.AddItems([new AddItemsParams(Guid.CreateVersion7(), 2, true)]);

      _repository.GetAsync(
          Arg.Any<Guid>(),
          Arg.Any<Guid>(),
          TestContext.Current.CancellationToken)
        .Returns(_list);

      _clock.GetCurrentTime().Returns(DateTime.UtcNow);
    }

    internal Guid? GetPublishedCheckoutId() {
      return _list.DomainEvents
        .OfType<ShoppingListCheckedOut>()
        .FirstOrDefault()?
        .CheckoutId;
    }
  }

  public CheckoutShoppingListEndpointTests() {
    handler = new FakeHandler(
      Substitute.For<IShoppingListRepository>(),
      Substitute.For<IClock>());
  }

  [Fact]
  public async Task Checkout_ReturnsAcceptedStatus() {
    handler.WithHappyPath();

    var context = new DefaultHttpContext {
      User = new ClaimsPrincipal(new ClaimsIdentity([
        new Claim(JwtRegisteredClaimNames.Sub, Guid.CreateVersion7().ToString()),
      ])),
    };

    IResult result = await CheckoutShoppingListEndpoint.CheckoutAsync(
      Guid.CreateVersion7(), context, handler, TestContext.Current.CancellationToken);

    int? statusCode = ((IStatusCodeHttpResult)result).StatusCode;
    Assert.Equal(StatusCodes.Status202Accepted, statusCode);
  }

  [Fact]
  public async Task Checkout_ReturnsCheckoutId() {
    handler.WithHappyPath();

    var context = new DefaultHttpContext {
      User = new ClaimsPrincipal(new ClaimsIdentity([
        new Claim(JwtRegisteredClaimNames.Sub, Guid.CreateVersion7().ToString()),
      ])),
    };

    IResult result = await CheckoutShoppingListEndpoint.CheckoutAsync(
      Guid.CreateVersion7(), context, handler, TestContext.Current.CancellationToken);

    CheckoutResponse? body = ((IValueHttpResult<CheckoutResponse>)result).Value;

    Guid? expectedCheckoutId = handler.GetPublishedCheckoutId();
    Assert.Equal(expectedCheckoutId, body!.CheckoutId);
  }
}