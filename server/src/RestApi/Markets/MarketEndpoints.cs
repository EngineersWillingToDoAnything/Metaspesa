using Metaspesa.RestApi.Brands.CreateBrand;
using Metaspesa.RestApi.Markets.AddSnapshot;
using Metaspesa.RestApi.Markets.CreateMarket;
using Metaspesa.RestApi.Markets.GetMarkets;
using Metaspesa.RestApi.Markets.GetProducts;

namespace Metaspesa.RestApi.Markets;

internal static class MarketEndpoints {
  public static IEndpointRouteBuilder MapMarketEndpoints(
    this IEndpointRouteBuilder endpoints
  ) {
    RouteGroupBuilder api = endpoints.MapGroup("/api/v1");
    api.MapGetMarketsEndpoint();
    api.MapQueryProductsEndpoint();
    api.MapSnapshotEndpoint();
    api.MapCreateMarketEndpoint();
    api.MapCreateBrandEndpoint();
    return endpoints;
  }
}