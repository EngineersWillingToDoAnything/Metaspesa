using Metaspesa.RestApi.Security;
using CheckoutUseCase = Metaspesa.Application.Shopping.CheckoutShoppingList;

namespace Metaspesa.RestApi.Shopping.CheckoutShoppingList;

internal static class CheckoutShoppingListEndpoint {
  public static IEndpointRouteBuilder MapCheckoutShoppingListEndpoint(
    this IEndpointRouteBuilder endpoints
  ) {
    endpoints.MapPost("/{listId:guid}/checkouts", CheckoutAsync)
      .WithName("CheckoutShoppingList")
      .Produces<CheckoutResponse>(StatusCodes.Status202Accepted)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .Produces(StatusCodes.Status401Unauthorized)
      .Produces(StatusCodes.Status403Forbidden)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .ProducesProblem(StatusCodes.Status409Conflict)
      .ProducesProblem(StatusCodes.Status500InternalServerError)
      .AddEndpointFilter<AllowedOriginFilter>();
    return endpoints;
  }

  /// <summary>Check out a shopping list.</summary>
  /// <remarks>Commit the checkout of checked items. Purchase creation is processed separately. Requires the Shopper role.</remarks>
  /// <param name="listId">ID of the shopping list to check out.</param>
  /// <param name="context">Authenticated request context.</param>
  /// <param name="handler">Checkout shopping list use case.</param>
  /// <param name="cancellationToken">Request cancellation token.</param>
  /// <response code="202">Checkout accepted; the response contains its checkout ID. This does not confirm that a purchase exists.</response>
  /// <response code="400">The list ID is invalid.</response>
  /// <response code="401">No valid authentication token was provided.</response>
  /// <response code="403">The authenticated user lacks the Shopper role.</response>
  /// <response code="404">The list was not found.</response>
  /// <response code="409">No active items are checked.</response>
  /// <response code="500">An unexpected server or database failure occurred.</response>
  internal static async Task<IResult> CheckoutAsync(
    Guid listId,
    HttpContext context,
    CheckoutUseCase.Handler handler,
    CancellationToken cancellationToken
  ) {
    var command = new CheckoutUseCase.Command(ShoppingUser.GetUid(context), listId);

    Guid checkoutId = await handler.Handle(command, cancellationToken);

    return Results.Json(new CheckoutResponse(checkoutId),
      statusCode: StatusCodes.Status202Accepted);
  }
}