using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Application.Markets;
using Metaspesa.Domain.Markets;
using NSubstitute;

namespace Metaspesa.Application.UnitTests.Markets;

public class GetMarketProductsHandlerTest {
  private readonly IProductRepository _repository;
  private readonly GetMarketProducts.Handler _handler;

  public GetMarketProductsHandlerTest() {
    _repository = Substitute.For<IProductRepository>();
    _handler = new GetMarketProducts.Handler(_repository);
  }

  [Fact]
  public async Task Handle_ReturnsProductsProvidedByRepository() {
    var filter = new GetMarketProductsFilter(null, [], null, new Pagination(1, 10));
    var expected = new PagedResult<CatalogProduct>([], 42);
    _repository.GetProductsAsync(filter, TestContext.Current.CancellationToken)
      .Returns(expected);

    PagedResult<CatalogProduct> result = await _handler
      .Handle(new GetMarketProducts.Query(filter), TestContext.Current.CancellationToken);

    Assert.Same(expected, result);
  }

  [Fact]
  public async Task Handle_PassesRequestedFilterToRepository() {
    var filter = new GetMarketProductsFilter(
      "Milk", [MarketId.Create(Guid.Parse("00000000-0000-7000-8000-000000000001")), MarketId.Create(Guid.Parse("00000000-0000-7000-8000-000000000002"))], "Brand",
      new Pagination(3, 10), CatalogSort.PriceDesc);

    await _handler
      .Handle(new GetMarketProducts.Query(filter), TestContext.Current.CancellationToken);

    await _repository.Received(1).GetProductsAsync(
      filter, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_PassesCancellationTokenToRepository() {
    var filter = new GetMarketProductsFilter(null, [], null, new Pagination(1, 10));
    using var cancellation = new CancellationTokenSource();

    await _handler
      .Handle(new GetMarketProducts.Query(filter), cancellation.Token);

    await _repository.Received(1).GetProductsAsync(
      Arg.Any<GetMarketProductsFilter>(), cancellation.Token);
  }

  [Fact]
  public async Task Handle_RejectsNullQuery() {
    await Assert.ThrowsAsync<ArgumentNullException>(() =>
      _handler.Handle(null!, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Handle_PropagatesRepositoryFailure() {
    var filter = new GetMarketProductsFilter(null, [], null, new Pagination(1, 10));
    _repository.GetProductsAsync(filter, TestContext.Current.CancellationToken)
      .Returns<Task<PagedResult<CatalogProduct>>>(_ =>
        throw new InvalidOperationException("Repository unavailable"));

    await Assert.ThrowsAsync<InvalidOperationException>(() =>
      _handler.Handle(new GetMarketProducts.Query(filter),
          TestContext.Current.CancellationToken));
  }
}