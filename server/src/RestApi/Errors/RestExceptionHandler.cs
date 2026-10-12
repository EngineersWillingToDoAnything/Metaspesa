using Metaspesa.Database.Exceptions;
using Metaspesa.Domain.Identity.Errors;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel.Errors;
using Metaspesa.Domain.Shopping.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Metaspesa.RestApi.Errors;

internal sealed partial class RestExceptionHandler(
  IProblemDetailsService problemDetailsService,
  ILogger<RestExceptionHandler> logger
) : IExceptionHandler {
  public async ValueTask<bool> TryHandleAsync(
    HttpContext httpContext,
    Exception exception,
    CancellationToken cancellationToken
  ) {
    (int status, string code, string title, string problemType) = exception switch {
      UsernameAlreadyExistsException identity => (
        StatusCodes.Status409Conflict,
        identity.Code,
        "Username already exists",
        "username-already-exists"),
      InvalidCredentialsException identity => (
        StatusCodes.Status401Unauthorized,
        identity.Code,
        "Invalid credentials",
        "invalid-credentials"),
      UnauthorizedAccessException => (
        StatusCodes.Status401Unauthorized,
        "Auth.InvalidUser", "Invalid user", "invalid-user"),
      ShoppingListNotFoundException shopping => (
        StatusCodes.Status404NotFound, shopping.Code,
        "Shopping list not found", "shopping-list-not-found"),
      ShoppingItemNotFoundException shopping => (
        StatusCodes.Status404NotFound, shopping.Code,
        "Shopping item not found", "shopping-item-not-found"),
      ShoppingProductFormatNotFoundException shopping => (
        StatusCodes.Status404NotFound, shopping.Code,
        "Product format not found", "product-format-not-found"),
      ShoppingListAlreadyExistsException shopping => (
        StatusCodes.Status409Conflict, shopping.Code,
        "Shopping list already exists", "shopping-list-already-exists"),
      DuplicateShoppingItemException shopping => (
        StatusCodes.Status409Conflict, shopping.Code,
        "Shopping item already exists", "shopping-item-already-exists"),
      EmptyPurchaseItemsException purchase => (
        StatusCodes.Status409Conflict, purchase.Code,
        "No checked items", "no-checked-items"),
      PurchasePriceSnapshotNotFoundException purchase => (
        StatusCodes.Status409Conflict, purchase.Code,
        "Price snapshot not found", "price-snapshot-not-found"),
      ShoppingDomainException shopping => (
        StatusCodes.Status400BadRequest, shopping.Code,
        shopping.Message, "shopping-validation"),
      IdentityDomainException identity => (
        StatusCodes.Status400BadRequest,
        identity.Code,
        identity.Message,
        "identity-validation"),
      DatabaseException => (
        StatusCodes.Status500InternalServerError,
        "Server.DatabaseError",
        "Database error",
        "database-error"),
      MarketDomainException market => (
        StatusCodes.Status400BadRequest, market.Code, market.Message, "market-validation"),
      DomainException domain => (
        StatusCodes.Status400BadRequest, "Ingestion.InvalidProduct",
        domain.Message, "ingestion-validation"),
      BadHttpRequestException request => (
        request.StatusCode, "Request.Invalid", request.Message, "invalid-request"),
      _ => (
        StatusCodes.Status500InternalServerError,
        "Server.InternalError",
        "Internal server error",
        "internal-error"),
    };

    if (status >= StatusCodes.Status500InternalServerError) {
      LogRequestFailure(logger, exception);
    }

    httpContext.Response.StatusCode = status;
    await problemDetailsService.WriteAsync(new ProblemDetailsContext {
      HttpContext = httpContext,
      Exception = exception,
      ProblemDetails = new ProblemDetails {
        Type = $"https://metaspesa.app/problems/{problemType}",
        Title = title,
        Status = status,
        Extensions = {
          ["code"] = code,
          ["traceId"] = httpContext.TraceIdentifier,
        },
      },
    });
    return true;
  }

  [LoggerMessage(LogLevel.Error, "REST request failed")]
  private static partial void LogRequestFailure(
    ILogger logger, Exception exception);
}