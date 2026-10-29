using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.UnitTests.Markets;

public class ProductIdentityTests {
  [Fact]
  public void Create_PreservesMarketId() {
    var marketId = Guid.Parse("00000000-0000-7000-8000-000000000001");
    var brandId = Guid.Parse("00000000-0000-7000-8000-000000000002");
    var observation = new ProductObservationParams(
      marketId, brandId, " Milk ", DateTime.UtcNow, []);

    var identity = ProductIdentity.Create(observation);

    Assert.Equal(new MarketId(marketId), identity.MarketId);
  }

  [Fact]
  public void Create_PreservesBrandId() {
    var marketId = Guid.Parse("00000000-0000-7000-8000-000000000001");
    var brandId = Guid.Parse("00000000-0000-7000-8000-000000000002");
    var observation = new ProductObservationParams(
      marketId, brandId, " Milk ", DateTime.UtcNow, []);

    var identity = ProductIdentity.Create(observation);

    Assert.Equal(new BrandId(brandId), identity.BrandId);
  }

  [Fact]
  public void Equality_DistinguishesDifferentMarket() {
    var marketId = new MarketId(Guid.Parse("00000000-0000-7000-8000-000000000001"));
    var brandId = new BrandId(Guid.Parse("00000000-0000-7000-8000-000000000002"));
    var first = new ProductIdentity(marketId, brandId, new ProductName("Milk"));
    var second = new ProductIdentity(new MarketId(Guid.Parse("00000000-0000-7000-8000-000000000099")), brandId, new ProductName("Milk"));

    Assert.NotEqual(first, second);
  }

  [Fact]
  public void Equality_DistinguishesDifferentBrand() {
    var marketId = new MarketId(Guid.Parse("00000000-0000-7000-8000-000000000001"));
    var brandId = new BrandId(Guid.Parse("00000000-0000-7000-8000-000000000002"));
    var first = new ProductIdentity(marketId, brandId, new ProductName("Milk"));
    var second = new ProductIdentity(marketId, new BrandId(Guid.Parse("00000000-0000-7000-8000-000000000099")), new ProductName("Milk"));

    Assert.NotEqual(first, second);
  }

  [Fact]
  public void Equality_DistinguishesDifferentProductName() {
    var marketId = new MarketId(Guid.Parse("00000000-0000-7000-8000-000000000001"));
    var brandId = new BrandId(Guid.Parse("00000000-0000-7000-8000-000000000002"));
    var first = new ProductIdentity(marketId, brandId, new ProductName("Milk"));
    var second = new ProductIdentity(marketId, brandId, new ProductName("Bread"));

    Assert.NotEqual(first, second);
  }

  [Fact]
  public void GetHashCode_MatchesEquivalentIdentity() {
    var marketId = new MarketId(Guid.Parse("00000000-0000-7000-8000-000000000001"));
    var brandId = new BrandId(Guid.Parse("00000000-0000-7000-8000-000000000002"));
    var first = new ProductIdentity(marketId, brandId, new ProductName("Milk"));
    var second = new ProductIdentity(marketId, brandId, new ProductName("MILK"));

    Assert.Equal(first.GetHashCode(), second.GetHashCode());
  }

  [Fact]
  public void Grouping_CombinesEquivalentProductIdentities() {
    var marketId = new MarketId(Guid.Parse("00000000-0000-7000-8000-000000000001"));
    var brandId = new BrandId(Guid.Parse("00000000-0000-7000-8000-000000000002"));
    var first = new ProductIdentity(marketId, brandId, new ProductName("Milk"));
    var second = new ProductIdentity(marketId, brandId, new ProductName("MILK"));
    ProductIdentity[] identities = [first, second];

    IEnumerable<IGrouping<ProductIdentity, ProductIdentity>> groups = identities.GroupBy(identity => identity);

    Assert.Single(groups);
  }
}