using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Application.UnitTests.Markets;

public class GetMarketProductsFilterTests {
  [Fact]
  public void Constructor_RejectsNullMarketIds() {
    var pagination = new Pagination(1, 24);

    Assert.Throws<ArgumentNullException>(() =>
      new GetMarketProductsFilter(null, null!, null, pagination));
  }

  [Fact]
  public void Constructor_RejectsNullPagination() {
    IReadOnlyCollection<MarketId> marketIds = [];

    Assert.Throws<ArgumentNullException>(() =>
      new GetMarketProductsFilter(null, marketIds, null, null!));
  }

  [Fact]
  public void Constructor_RejectsDefaultMarketIdBecauseItIsNotPositive() =>
    Assert.Throws<InvalidMarketIdException>(() =>
      new GetMarketProductsFilter(null, [default], null,
        new Pagination(1, 24)));

  [Fact]
  public void Constructor_AcceptsNoMarketIds() {
    var filter = new GetMarketProductsFilter(
      null, [], null, new Pagination(1, 24));

    Assert.Empty(filter.MarketIds);
  }

  [Fact]
  public void Constructor_KeepsAllSelectedMarketIds() {
    MarketId[] marketIds = [MarketId.Create(Guid.Parse("00000000-0000-7000-8000-000000000002")), MarketId.Create(Guid.Parse("00000000-0000-7000-8000-000000000004"))];

    var filter = new GetMarketProductsFilter(
      null, marketIds, null, new Pagination(1, 24));

    Assert.Equal(marketIds, filter.MarketIds);
  }

  [Fact]
  public void Constructor_CopiesMarketIdsSoLaterInputChangesDoNotAlterFilter() {
    MarketId[] marketIds = [MarketId.Create(Guid.Parse("00000000-0000-7000-8000-000000000002"))];
    var filter = new GetMarketProductsFilter(
      null, marketIds, null, new Pagination(1, 24));

    marketIds[0] = MarketId.Create(Guid.Parse("00000000-0000-7000-8000-000000000004"));

    Assert.Equal<MarketId>([MarketId.Create(Guid.Parse("00000000-0000-7000-8000-000000000002"))], filter.MarketIds);
  }

  [Fact]
  public void Constructor_KeepsProductNameSearchSegment() {
    var filter = new GetMarketProductsFilter(
      "Milk", [], null, new Pagination(1, 24));

    Assert.Equal("Milk", filter.NameSegment);
  }

  [Fact]
  public void Constructor_KeepsBrandNameSearchSegment() {
    var filter = new GetMarketProductsFilter(
      null, [], "Brand", new Pagination(1, 24));

    Assert.Equal("Brand", filter.BrandNameSegment);
  }

  [Fact]
  public void Constructor_UsesNameSortByDefault() {
    var filter = new GetMarketProductsFilter(
      null, [], null, new Pagination(1, 24));

    Assert.Equal(CatalogSort.Name, filter.Sort);
  }

  [Fact]
  public void Constructor_KeepsRequestedPriceSort() {
    var filter = new GetMarketProductsFilter(
      null, [], null, new Pagination(1, 24), CatalogSort.PriceDesc);

    Assert.Equal(CatalogSort.PriceDesc, filter.Sort);
  }

  [Fact]
  public void Constructor_KeepsValidatedPagination() {
    var pagination = new Pagination(3, 10);

    var filter = new GetMarketProductsFilter(null, [], null, pagination);

    Assert.Same(pagination, filter.Pagination);
  }
}