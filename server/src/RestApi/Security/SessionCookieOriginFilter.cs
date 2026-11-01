using Metaspesa.Infrastructure;
using Microsoft.Extensions.Options;

namespace Metaspesa.RestApi.Security;

internal sealed class SessionCookieOriginFilter(
  IOptions<SessionCookieOptions> options,
  AllowedOriginFilter allowedOriginFilter
) : IEndpointFilter {
  public ValueTask<object?> InvokeAsync(
    EndpointFilterInvocationContext context,
    EndpointFilterDelegate next
  ) {
    bool hasSessionCookie = context.HttpContext.Request.Cookies.TryGetValue(
      options.Value.CookieName, out string? token) && !string.IsNullOrEmpty(token);

    return hasSessionCookie
      ? allowedOriginFilter.InvokeAsync(context, next)
      : next(context);
  }
}