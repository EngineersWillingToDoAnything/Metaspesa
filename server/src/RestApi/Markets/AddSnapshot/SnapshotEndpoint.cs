using System.Globalization;
using Metaspesa.Application.Markets;
using Metaspesa.Domain.Identity;
using Metaspesa.Infrastructure;

namespace Metaspesa.RestApi.Markets.AddSnapshot;

internal static class SnapshotEndpoint {
  public static IEndpointRouteBuilder MapSnapshotEndpoint(
    this IEndpointRouteBuilder endpoints
  ) {
    endpoints.MapPost("/markets/{marketName}/snapshots/{date}", HandleAsync)
      .WithName("AddMarketSnapshot")
      .Produces(StatusCodes.Status204NoContent)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .Produces(StatusCodes.Status401Unauthorized)
      .Produces(StatusCodes.Status403Forbidden)
      .ProducesProblem(StatusCodes.Status409Conflict)
      .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
      .ProducesProblem(StatusCodes.Status500InternalServerError)
      .RequireAuthorization(policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(nameof(Role.ProductManager)));
    return endpoints;
  }

  /// <summary>Import a market price snapshot.</summary>
  /// <remarks>Import product observations for a market and date. Requires authentication with the ProductManager role.</remarks>
  /// <param name="marketName">Name of the observed market.</param>
  /// <param name="date">Observation date in YYYY-MM-DD format, on or after 2023-01-01.</param>
  /// <param name="handler">Market import use case.</param>
  /// <param name="cancellationToken">Request cancellation token.</param>
  /// <param name="request">Product observations for the market and date.</param>
  /// <response code="204">Snapshot imported; no response body.</response>
  /// <response code="400">The date or a product observation is invalid.</response>
  /// <response code="401">No valid authentication token was provided.</response>
  /// <response code="403">The authenticated user lacks the ProductManager role.</response>
  /// <response code="409">The market is not configured in master data.</response>
  /// <response code="422">A product references an unknown brand.</response>
  /// <response code="500">An unexpected server or database failure occurred.</response>
  internal static async Task<IResult> HandleAsync(
    string marketName,
    string date,
    SnapshotRequest request,
    AddMarketProducts.Handler handler,
    CancellationToken cancellationToken
  ) {
    if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
      DateTimeStyles.None, out DateOnly registeredAt)) {
      throw new BadHttpRequestException("Snapshot date must use YYYY-MM-DD.");
    }
    AddMarketProducts.Command command = ToCommand(marketName, registeredAt, request);
    await handler.Handle(command, cancellationToken);
    return Results.NoContent();
  }

  internal static AddMarketProducts.Command ToCommand(
    string marketName, DateOnly registeredAt, SnapshotRequest request
  ) {
    if (request.Items is null || request.Items.Any(item => item is null)) {
      throw new BadHttpRequestException("Items must be an array of product observations.");
    }
    var products = new List<AddMarketProducts.CommandProduct>();
    foreach (SnapshotItem? item in request.Items) {
      Uri? imageUrl = null;
      if (!string.IsNullOrEmpty(item!.ImageUrl) &&
        !Uri.TryCreate(item.ImageUrl, UriKind.Absolute, out imageUrl)) {
        throw new BadHttpRequestException("Image URL must be an absolute URL.");
      }
      products.Add(new AddMarketProducts.CommandProduct(
        item.Name is null ? null : TextSanitizer.Sanitize(item.Name),
        item.Price,
        item.Quantity,
        item.UnitOfMeasure is null ? null : TextSanitizer.Sanitize(item.UnitOfMeasure),
        TextSanitizer.Sanitize(marketName),
        item.BrandName is null ? null : TextSanitizer.Sanitize(item.BrandName),
        imageUrl));
    }
    return new AddMarketProducts.Command(products, registeredAt);
  }
}