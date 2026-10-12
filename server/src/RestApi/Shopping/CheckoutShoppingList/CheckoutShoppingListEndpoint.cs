using Metaspesa.RestApi.Security;
using CheckoutUseCase = Metaspesa.Application.Shopping.CheckoutShoppingList;

namespace Metaspesa.RestApi.Shopping.CheckoutShoppingList;

internal static class CheckoutShoppingListEndpoint {
  public static IEndpointRouteBuilder MapCheckoutShoppingListEndpoint(
    this IEndpointRouteBuilder endpoints
  ) {
    endpoints.MapPost("/{listId:guid}/checkouts", CheckoutAsync)
      .WithName("CheckoutShoppingList")
      .Produces<PurchaseResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .ProducesProblem(StatusCodes.Status409Conflict)
      .Produces(StatusCodes.Status401Unauthorized)
      .Produces(StatusCodes.Status403Forbidden)
      .ProducesProblem(StatusCodes.Status500InternalServerError)
      .AddEndpointFilter<AllowedOriginFilter>();
    return endpoints;
  }

  /// <summary>Check out a shopping list.</summary>
  /// <remarks>Record a purchase from checked items at their latest observed prices, then clear their checked states. Requires the Shopper role.</remarks>
  /// <param name="listId">ID of the shopping list to check out.</param>
  /// <param name="context">Authenticated request context.</param>
  /// <param name="handler">Checkout shopping list use case.</param>
  /// <param name="cancellationToken">Request cancellation token.</param>
  /// <response code="201">Purchase recorded; the response contains its ID.</response>
  /// <response code="400">The list ID is invalid.</response>
  /// <response code="404">The list was not found.</response>
  /// <response code="409">No items are checked or a checked item has no price snapshot.</response>
  /// <response code="401">No valid authentication token was provided.</response>
  /// <response code="403">The authenticated user lacks the Shopper role.</response>
  /// <response code="500">An unexpected server or database failure occurred.</response>
  internal static async Task<IResult> CheckoutAsync(
    Guid listId, HttpContext context,
    CheckoutUseCase.Handler handler,
    CancellationToken cancellationToken
  ) {
    Guid purchaseId = await handler.Handle(new CheckoutUseCase.Command(
      ShoppingUser.GetUid(context), listId), cancellationToken);
    return Results.Json(new PurchaseResponse(purchaseId),
      statusCode: StatusCodes.Status201Created);
  }
}