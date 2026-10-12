using System.Security.Cryptography;
using System.Text;
using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Users;
using Metaspesa.Infrastructure.Events;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Metaspesa.Infrastructure;

public static class InfrastructureDependencyInjection {
  public static IServiceCollection AddInfrastructure(
    this IServiceCollection services
  ) {
    services.AddOpenTelemetry()
      .WithTracing(tracing => tracing.AddSource(SecretsLoaderWorker.ActivitySourceName));

    services.AddSingleton<IClock, SystemClock>();
    services.AddSingleton<ISecretVault, LocalSecretVault>();
    services.AddHostedService<SecretsLoaderWorker>();

    services.AddSingleton<ITokenProvider, JwtTokenProvider>();
    services.AddSingleton<IHasher, Pbkdf2Hasher>();

    return services.AddSession()
      .AddEventHandling();
  }

  private static IServiceCollection AddEventHandling(
    this IServiceCollection services
  ) {
    services.AddSingleton<InMemoryDomainEventPublisher>();
    services.AddSingleton<IDomainEventPublisher>(provider =>
      provider.GetRequiredService<InMemoryDomainEventPublisher>());

    return services;
  }

  private static IServiceCollection AddSession(
    this IServiceCollection services
  ) {
    services.AddOptionsWithValidateOnStart<JwtOptions>()
      .BindConfiguration("Jwt")
      .Validate(o => !string.IsNullOrWhiteSpace(o.KeySecretName), "Jwt:KeySecretName must be provided")
      .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Jwt:Issuer must be provided")
      .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience must be provided")
      .Validate(o => o.ExpirationMinutes > 0, "Jwt:ExpirationMinutes must be greater than 0");
    services.AddOptionsWithValidateOnStart<SessionCookieOptions>()
      .BindConfiguration("BrowserSession")
      .Validate(
        options => !string.IsNullOrWhiteSpace(options.CookieName),
        "BrowserSession:CookieName must be provided");

    services
      .AddAuthorization()
      .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer();

    services
      .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
      .Configure<IOptions<JwtOptions>, IOptions<SessionCookieOptions>>(
        (bearerOptions, jwtOptions, sessionOptions) => {
          bearerOptions.TokenValidationParameters = new TokenValidationParameters {
            ValidIssuer = jwtOptions.Value.Issuer,
            ValidAudience = jwtOptions.Value.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
              Encoding.UTF8.GetBytes(jwtOptions.Value.Key!)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
          };
          bearerOptions.Events = new JwtBearerEvents {
            OnMessageReceived = context => {
              if (string.IsNullOrEmpty(context.Token) &&
                context.Request.Cookies.TryGetValue(
                  sessionOptions.Value.CookieName, out string? token)) {
                context.Token = token;
              }
              return Task.CompletedTask;
            },
          };
        });

    return services;
  }
}
