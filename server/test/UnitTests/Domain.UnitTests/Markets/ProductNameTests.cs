using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.UnitTests.Markets;

public class ProductNameTests {
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" ")]
  public void Create_RejectsMissingName(string? value) {
    void action() => ProductName.Create(value!);

    Assert.Throws<InvalidProductNameException>(action);
  }

  [Fact]
  public void Create_TrimsName() {
    var name = ProductName.Create(" Milk ");

    Assert.Equal("Milk", name.Value);
  }

  [Fact]
  public void Constructor_PreservesDisplayCasing() {
    var name = new ProductName("Whole MILK");

    Assert.Equal("Whole MILK", name.Value);
  }

  [Theory]
  [InlineData("Milk", "MILK")]
  [InlineData("Caf\u00e9", "CAF\u00c9")]
  [InlineData("\u00c1gua", "\u00e1gua")]
  public void Equality_IgnoresNameCasing(string first, string second) {
    var left = new ProductName(first);
    var right = new ProductName(second);

    Assert.Equal(left, right);
  }

  [Theory]
  [InlineData("Milk", "Bread")]
  [InlineData("Cafe", "Caf\u00e9")]
  public void Equality_DistinguishesNames(string first, string second) {
    var left = new ProductName(first);
    var right = new ProductName(second);

    Assert.NotEqual(left, right);
  }

  [Fact]
  public void GetHashCode_IgnoresNameCasing() {
    var first = new ProductName("Milk");
    var second = new ProductName("MILK");

    Assert.Equal(first.GetHashCode(), second.GetHashCode());
  }

  [Fact]
  public void GetHashCode_ReturnsZeroForDefault() {
    var name = default(ProductName);

    Assert.Equal(0, name.GetHashCode());
  }

  [Fact]
  public void ToString_ReturnsDisplayName() {
    var name = ProductName.Create(" Milk ");

    Assert.Equal("Milk", name.ToString());
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" Milk ")]
  public void Constructor_PreservesRawName(string? value) {
    var name = new ProductName(value!);

    Assert.Equal(value, name.Value);
  }

  [Fact]
  public void Equality_DistinguishesDefaultNameFromNamedProduct() {
    ProductName first = default;
    var second = new ProductName("Milk");

    Assert.NotEqual(first, second);
  }
}