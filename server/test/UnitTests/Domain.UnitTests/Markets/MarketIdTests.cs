using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.UnitTests.Markets;

public static class MarketIdTests {
  [Fact(DisplayName = "Creates and compares market ids by value")]
  public static void MarketId_CreatedAndEqual_WhenValueIsPositive() {
    var rawId = Guid.Parse("00000000-0000-7000-8000-000000000001");
    var id = MarketId.Create(rawId);

    Assert.Equal(rawId, id.Value);
    Assert.Equal(id, MarketId.Create(rawId));
  }

  [Fact(DisplayName = "Rejects empty market id")]
  public static void MarketId_ThrowsSpecificException_WhenValueIsInvalid() =>
    Assert.Throws<InvalidMarketIdException>(() => MarketId.Create(Guid.Empty));
}