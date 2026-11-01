using Metaspesa.Domain.Identity;
using Metaspesa.Infrastructure;
using Metaspesa.RestApi.Security;
using UseCase = Metaspesa.Application.Markets.CreateMarket;

namespace Metaspesa.RestApi.Markets.CreateMarket;

internal static partial class CreateMarketEndpoint {
  public static IEndpointRouteBuilder MapCreateMarketEndpoint(this IEndpointRouteBuilder endpoints) {
    endpoints.MapPost("/markets", HandleAsync)
      .WithName("CreateMarket")
      .Produces<CreateMarketResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .Produces(StatusCodes.Status401Unauthorized)
      .Produces(StatusCodes.Status403Forbidden)
      .ProducesProblem(StatusCodes.Status409Conflict)
      .ProducesProblem(StatusCodes.Status500InternalServerError)
      .RequireAuthorization(policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(nameof(Role.CatalogAdmin)))
      .AddEndpointFilter<SessionCookieOriginFilter>();
    return endpoints;
  }

  /// <summary>Create a Market.</summary>
  /// <remarks>
  /// Requires the CatalogAdmin role using a bearer token or session cookie.
  /// Session-cookie requests must include an allowed Origin header.
  /// Names are case-insensitive and accent-sensitive.
  /// </remarks>
  /// <response code="201">Market created with a server-generated ID.</response>
  /// <response code="400">Invalid name, logo URL, or request properties.</response>
  /// <response code="401">A valid bearer token or session cookie is required.</response>
  /// <response code="403">The CatalogAdmin role is missing, or the session-cookie origin is not allowed.</response>
  /// <response code="409">A Market with this name already exists.</response>
  /// <response code="500">An unexpected server or database failure occurred.</response>
  internal static async Task<IResult> HandleAsync(
    CreateMarketRequest request,
    UseCase.Handler handler,
    CancellationToken cancellationToken
  ) {
    var command = new UseCase.Command(
      request.Name is null ? null! : TextSanitizer.Sanitize(request.Name),
      request.LogoUrl);

    Guid id = await handler.Handle(command, cancellationToken);

    return Results.Created((Uri?)null, new CreateMarketResponse(id));
  }
}