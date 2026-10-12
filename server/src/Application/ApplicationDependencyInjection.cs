using System.Diagnostics;
using Metaspesa.Application.Identity;
using Metaspesa.Application.Markets;
using Metaspesa.Application.Shopping;
using Microsoft.Extensions.DependencyInjection;

namespace Metaspesa.Application;

public static class ApplicationDependencyInjection {
#pragma warning disable CA1034 // Nested types should not be visible
  extension(IServiceCollection services) {
#pragma warning restore CA1034 // Nested types should not be visible
    public IServiceCollection AddApplication() {
      Debug.Assert(services != null);

      return services.AddIdentityUseCases()
        .AddMarketUseCases()
        .AddShoppingUseCases();
    }

    private IServiceCollection AddIdentityUseCases() {
      services.AddScoped<RegisterUser.Handler>();
      services.AddScoped<LoginUser.Handler>();

      return services;
    }

    private IServiceCollection AddMarketUseCases() {
      services.AddScoped<AddMarketProducts.Handler>();
      services.AddScoped<GetMarketProducts.Handler>();
      services.AddScoped<GetMarkets.Handler>();

      return services;
    }

    private IServiceCollection AddShoppingUseCases() {
      services.AddScoped<GetShoppingList.Handler>();
      services.AddScoped<GetShoppingListSummaries.Handler>();

      services.AddScoped<CreateShoppingList.Handler>();
      services.AddScoped<AddItemsToList.Handler>();
      services.AddScoped<UpdateItem.Handler>();
      services.AddScoped<UpdateShoppingList.Handler>();
      services.AddScoped<RemoveItem.Handler>();

      services.AddScoped<CheckoutShoppingList.Handler>();
      return services;
    }
  }
}