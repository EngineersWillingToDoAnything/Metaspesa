using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Application.Markets;
using Metaspesa.Domain.Markets;

namespace Metaspesa.RestApi.Markets.GetProducts;

internal static class GetProductsEndpoint {
  public static IEndpointRouteBuilder MapQueryProductsEndpoint(
    this IEndpointRouteBuilder endpoints
  ) {
    endpoints.MapMethods("/products", [HttpMethods.Query], GetProductsAsync)
      .WithName("GetProducts")
      .Produces<ProductPageResponse>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status500InternalServerError);
    return endpoints;
  }

  /// <summary>Search catalog products.</summary>
  /// <remarks>Search market-owned products with filters supplied as JSON query content. Each result includes formats with their latest observed prices. Pagination counts products, not formats.</remarks>
  /// <param name="request">Optional product filters, sorting, and pagination.</param>
  /// <param name="handler">Product catalog query handler.</param>
  /// <param name="cancellationToken">Request cancellation token.</param>
  /// <response code="200">Matching products and pagination counts. An empty page has no items.</response>
  /// <response code="400">A filter, market ID, page value, or sort value is invalid.</response>
  /// <response code="500">An unexpected server or database failure occurred.</response>
  internal static async Task<IResult> GetProductsAsync(
    GetProductsRequest request,
    GetMarketProducts.Handler handler,
    CancellationToken cancellationToken
  ) {
    GetMarketProductsFilter filter = ParseFilter(request);
    PagedResult<CatalogProduct> result = await handler.Handle(
      new GetMarketProducts.Query(filter), cancellationToken);
    return Results.Ok(new ProductPageResponse(
      [.. result.Values.Select(ToProduct)], filter.Pagination.Index,
      filter.Pagination.Size, result.TotalCount,
      (int)Math.Ceiling((double)result.TotalCount / filter.Pagination.Size)));
  }

  internal static GetMarketProductsFilter ParseFilter(GetProductsRequest request) {
    Pagination pagination;
    try {
      pagination = new Pagination(request.Page ?? 1, request.PageSize ?? 24);
    } catch (ArgumentOutOfRangeException exception) {
      throw new BadHttpRequestException(exception.Message);
    }
    MarketId[] marketIds = request.MarketId is null
      ? []
      : [.. request.MarketId.Select(id => new MarketId(id))];
    CatalogSort sort = request.Sort switch {
      null or "name" => CatalogSort.Name,
      "priceAsc" => CatalogSort.PriceAsc,
      "priceDesc" => CatalogSort.PriceDesc,
      _ => throw new BadHttpRequestException("Sort must be name, priceAsc, or priceDesc."),
    };
    return new GetMarketProductsFilter(
      request.Query, marketIds, request.Brand, pagination, sort);
  }

  private static ProductResponse ToProduct(CatalogProduct product) => new(
    product.Id, product.Name, product.Brand,
    MarketResponse.FromSummary(product.Market),
    [.. product.Formats.Select(format => new FormatResponse(
      format.Id, new QuantityResponse(format.Quantity, format.Unit),
      new MoneyResponse(format.Price, format.Currency),
      format.ObservedOn) { ImageUrl = format.ImageUrl?.ToString() })]);
}