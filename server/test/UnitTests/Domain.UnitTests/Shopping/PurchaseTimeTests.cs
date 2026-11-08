using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class PurchaseTimeTests {
  private sealed class InvalidPurchaseTimes() : TheoryData<DateTime>(
    default,
    new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Local),
    new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Unspecified)
  );

  [Fact]
  public void Create_PreservesDateTime() {
    // Arrange
    var date = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc);

    // Act
    var purchaseTime = PurchaseTime.Create(date);

    // Assert
    Assert.Equal(date, purchaseTime.Value);
  }

  [Theory]
  [ClassData<InvalidPurchaseTimes>]
  public void Create_RejectsInvalidDateTime(DateTime time) {
    // Act
    Exception? failure = Record.Exception(() => PurchaseTime.Create(time));

    // Assert
    Assert.IsType<InvalidPurchasedAtException>(failure);
  }
}