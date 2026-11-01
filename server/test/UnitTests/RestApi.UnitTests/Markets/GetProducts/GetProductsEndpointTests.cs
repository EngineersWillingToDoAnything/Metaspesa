using System.Text.Json;
using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Application.Markets;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.RestApi.Markets.GetProducts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Metaspesa.RestApi.UnitTests.Markets.GetProducts;

public static class GetProductsEndpointTests {
  private static readonly Guid MarketId = Guid.CreateVersion7();
  private static readonly Guid ProductId = Guid.CreateVersion7();
  private static readonly Guid FormatId = Guid.CreateVersion7();
  private static readonly Guid FirstFilterMarketId = Guid.CreateVersion7();
  private static readonly Guid SecondFilterMarketId = Guid.CreateVersion7();

  [Fact]
  public static void ParseFilter_UsesDocumentedDefaults() {
    GetMarketProductsFilter filter = GetProductsEndpoint.ParseFilter(
      new GetProductsRequest());

    Assert.Equal(1, filter.Pagination.Index);
    Assert.Equal(24, filter.Pagination.Size);
    Assert.Equal(CatalogSort.Name, filter.Sort);
    Assert.Empty(filter.MarketIds);
  }

  [Fact]
  public static void ParseFilter_AcceptsMarketsAndSort() {
    var request = new GetProductsRequest {
      Query = "milk",
      MarketId = [FirstFilterMarketId, SecondFilterMarketId],
      Brand = "Brand",
      Page = 3,
      PageSize = 10,
      Sort = "priceDesc",
    };

    GetMarketProductsFilter filter = GetProductsEndpoint.ParseFilter(request);

    Assert.Equal([new MarketId(FirstFilterMarketId),
      new MarketId(SecondFilterMarketId)], filter.MarketIds);
    Assert.Equal(CatalogSort.PriceDesc, filter.Sort);
    Assert.Equal(3, filter.Pagination.Index);
    Assert.Equal(10, filter.Pagination.Size);
    Assert.Equal("milk", filter.NameSegment);
    Assert.Equal("Brand", filter.BrandNameSegment);
  }

  [Fact]
  public static void ParseFilter_RejectsInvalidPagination() {
    var request = new GetProductsRequest { Page = 0, PageSize = 101 };

    Assert.Throws<BadHttpRequestException>(() => GetProductsEndpoint.ParseFilter(request));
  }

  [Fact]
  public static void ParseFilter_RejectsInvalidSort() {
    var request = new GetProductsRequest { Sort = "newest" };

    Assert.Throws<BadHttpRequestException>(() => GetProductsEndpoint.ParseFilter(request));
  }

  [Fact]
  public static void ParseFilter_UsesDomainExceptionForInvalidMarketId() {
    var request = new GetProductsRequest { MarketId = [Guid.Empty] };

    Assert.Throws<InvalidMarketIdException>(() => GetProductsEndpoint.ParseFilter(request));
  }

  [Fact]
  public static async Task MapQueryProductsEndpoint_MapsOnlyQueryVerb() {
    WebApplicationBuilder builder = WebApplication.CreateBuilder();
    builder.Services.AddScoped<GetMarketProducts.Handler>();
    await using WebApplication app = builder.Build();

    app.MapQueryProductsEndpoint();

    RouteEndpoint endpoint = Assert.IsType<RouteEndpoint>(
      ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
        .Single());
    HttpMethodMetadata metadata = Assert.IsType<HttpMethodMetadata>(
      endpoint.Metadata.GetMetadata<IHttpMethodMetadata>());
    Assert.Equal([HttpMethods.Query], metadata.HttpMethods);
  }

  [Fact]
  public static async Task GetProducts_SerializesObservationAsCalendarDate() {
    IProductRepository repository = Substitute.For<IProductRepository>();
    repository.GetProductsAsync(Arg.Any<GetMarketProductsFilter>(), Arg.Any<CancellationToken>())
      .Returns(new PagedResult<CatalogProduct>([
        new CatalogProduct(ProductId, "Whole milk", "Hacendado",
          new MarketSummary(MarketId, "Mercadona", null), [
            new CatalogFormat(FormatId, 1, "l", 1.04m, "EUR", null, new DateOnly(2026, 8, 20)),
          ]),
      ], 1));
    var context = new DefaultHttpContext {
      RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
    };
    context.Response.Body = new MemoryStream();

    IResult response = await GetProductsEndpoint.GetProductsAsync(
      new GetProductsRequest(), new GetMarketProducts.Handler(repository), TestContext.Current.CancellationToken);
    await response.ExecuteAsync(context);
    context.Response.Body.Position = 0;
    using JsonDocument json = await JsonDocument.ParseAsync(context.Response.Body,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal("2026-08-20", json.RootElement.GetProperty("items")[0]
      .GetProperty("formats")[0].GetProperty("observedOn").GetString());
  }

  [Fact]
  public static async Task GetProducts_ReturnsFlatPageAndOmitsMissingImages() {
    IProductRepository repository = Substitute.For<IProductRepository>();
    repository.GetProductsAsync(Arg.Any<GetMarketProductsFilter>(), Arg.Any<CancellationToken>())
      .Returns(new PagedResult<CatalogProduct>([
        new CatalogProduct(ProductId, "Whole milk", "Hacendado",
          new MarketSummary(MarketId, "Mercadona", null), [
            new CatalogFormat(FormatId, 1, "l", 1.04m, "EUR", null,
              new DateOnly(2026, 8, 20)),
          ]),
      ], 25));
    var context = new DefaultHttpContext {
      RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
    };
    context.Response.Body = new MemoryStream();

    IResult response = await GetProductsEndpoint.GetProductsAsync(
      new GetProductsRequest(),
      new GetMarketProducts.Handler(repository),
      TestContext.Current.CancellationToken);
    await response.ExecuteAsync(context);
    context.Response.Body.Position = 0;
    using JsonDocument json = await JsonDocument.ParseAsync(context.Response.Body,
      cancellationToken: TestContext.Current.CancellationToken);

    JsonElement root = json.RootElement;
    Assert.Equal(200, context.Response.StatusCode);
    Assert.Equal(1, root.GetProperty("page").GetInt32());
    Assert.Equal(24, root.GetProperty("pageSize").GetInt32());
    Assert.Equal(25, root.GetProperty("totalItems").GetInt32());
    Assert.Equal(2, root.GetProperty("totalPages").GetInt32());
    JsonElement product = root.GetProperty("items")[0];
    Assert.Equal(ProductId, product.GetProperty("id").GetGuid());
    Assert.Equal(MarketId, product.GetProperty("market").GetProperty("id").GetGuid());
    Assert.False(product.GetProperty("market").TryGetProperty("logoUrl", out _));
    JsonElement format = product.GetProperty("formats")[0];
    Assert.Equal(FormatId, format.GetProperty("id").GetGuid());
    Assert.Equal("EUR", format.GetProperty("currentPrice").GetProperty("currency").GetString());
    Assert.False(format.TryGetProperty("imageUrl", out _));
  }

}