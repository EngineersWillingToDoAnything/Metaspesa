using System.Text.Json.Serialization;
using Metaspesa.RestApi.Errors;
using Metaspesa.RestApi.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

namespace Metaspesa.RestApi;

internal static class ServiceCollectionExtensions {
  public static IServiceCollection AddRestApi(
    this IServiceCollection services, IConfiguration configuration
  ) {
    string[] allowedOrigins = configuration
      .GetSection("BrowserSecurity:AllowedOrigins")
      .Get<string[]>() ?? [];

    services.AddProblemDetails();
    services.ConfigureHttpJsonOptions(options =>
      options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
    services.AddOpenApi(options => {
      options.AddDocumentTransformer((document, _, _) => {
        document.Info.Title = "Metaspesa REST API";
        document.Info.Description =
          "Errors use ProblemDetails with a stable code and traceId for support.";
        document.Servers = [new OpenApiServer { Url = "/" }];
        foreach (IOpenApiRequestBody body in document.Paths.Values
          .Where(path => path.Operations is not null)
          .SelectMany(path => path.Operations!.Values)
          .Select(operation => operation.RequestBody)
          .OfType<IOpenApiRequestBody>()) {
          IOpenApiSchema? bodySchema = body.Content?
            .Values.FirstOrDefault()?.Schema;
          string? description = bodySchema is OpenApiSchemaReference reference
            ? reference.Target?.Description : bodySchema?.Description;
          if (!string.IsNullOrWhiteSpace(description)) {
            body.Description = description;
          }
        }
        return Task.CompletedTask;
      });
      options.AddSchemaTransformer((schema, context, _) => {
        if (context.JsonTypeInfo.Type == typeof(ProblemDetails)) {
          schema.Description =
            "Error response with a machine-readable code and request trace identifier.";
          schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
          schema.Properties["code"] = new OpenApiSchema {
            Type = JsonSchemaType.String,
            Description = "Stable machine-readable error code.",
          };
          schema.Properties["traceId"] = new OpenApiSchema {
            Type = JsonSchemaType.String,
            Description = "Request trace identifier for support and logs.",
          };
          if (schema.Properties.TryGetValue("type", out IOpenApiSchema? type)) {
            type.Description = "URI identifying the class of problem.";
          }
          if (schema.Properties.TryGetValue("title", out IOpenApiSchema? title)) {
            title.Description = "Short human-readable reason for the failure.";
          }
          if (schema.Properties.TryGetValue("status", out IOpenApiSchema? status)) {
            status.Description = "HTTP status code of this response.";
          }
          if (schema.Properties.TryGetValue("detail", out IOpenApiSchema? detail)) {
            detail.Description = "Additional detail when available.";
          }
          if (schema.Properties.TryGetValue("instance", out IOpenApiSchema? instance)) {
            instance.Description = "Request URI when available.";
          }
          schema.Required ??= new HashSet<string>();
          schema.Required.Add("code");
          schema.Required.Add("traceId");
        }
        return Task.CompletedTask;
      });
    });
    services.AddExceptionHandler<RestExceptionHandler>();
    services.AddTransient<AllowedOriginFilter>();
    services.AddRequestDecompression();
    services.AddOptionsWithValidateOnStart<BrowserSecurityOptions>()
      .BindConfiguration("BrowserSecurity")
      .Validate(
        options => options.AllowedOrigins.Length > 0,
        "At least one browser origin must be configured");
    services.AddCors(options => options.AddDefaultPolicy(policy =>
      policy.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

    return services;
  }
}