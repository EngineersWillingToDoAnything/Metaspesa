using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.UnitTests.Markets;

public static class ProductTests {
  private static readonly ImageUrl Image =
    new(new Uri("https://example.com/product.png"));

  [Fact(DisplayName = "Product references market by id and owns formats")]
  public static void Product_Created_WithBoundedFormats() {
    var format = new ProductFormat(
      new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-00000000000a")),
      Quantity.Create(1, "g"),
      Image);
    Product product = CreateProduct([format]);

    Assert.Equal(new MarketId(Guid.Parse("00000000-0000-7000-8000-000000000001")), product.MarketId);
    Assert.Equal(format, Assert.Single(product.Formats));
    Assert.DoesNotContain(
      typeof(Product).GetProperties(),
      property => property.PropertyType == typeof(Market));
    Assert.DoesNotContain(
      typeof(Product).GetProperties(),
      property => property.PropertyType == typeof(PriceSnapshot));
  }

  [Fact(DisplayName = "Adds a product format")]
  public static void Product_AddFormat_AddsValidFormat() {
    Product product = CreateProduct();
    var id = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-00000000000a"));
    var quantity = Quantity.Create(1, "g");

    product.AddFormat(id, quantity, Image);

    ProductFormat format = Assert.Single(product.Formats);
    Assert.Equal(id, format.Id);
    Assert.Equal(quantity, format.Quantity);
    Assert.Equal(Image, format.ImageUrl);
  }

  [Fact(DisplayName = "Rejects duplicate product format without mutation")]
  public static void Product_AddFormat_ThrowsSpecificException_WhenIdExists() {
    var id = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-00000000000a"));
    var original = new ProductFormat(
      id,
      Quantity.Create(1, "g"),
      Image);
    Product product = CreateProduct([original]);

    Assert.Throws<DuplicateProductFormatException>(
      () => product.AddFormat(
        id,
        Quantity.Create(2, "g"),
        new ImageUrl(new Uri("https://example.com/other.png"))));
    Assert.Same(original, Assert.Single(product.Formats));
  }

  [Fact(DisplayName = "Updates an owned product format")]
  public static void Product_UpdateFormat_UpdatesValues() {
    var id = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-00000000000a"));
    var format = new ProductFormat(
      id,
      Quantity.Create(1, "g"),
      Image);
    Product product = CreateProduct([format]);
    var quantity = Quantity.Create(2, "g");
    var image = new ImageUrl(new Uri("https://example.com/other.png"));

    product.UpdateFormat(id, quantity, image);

    Assert.Equal(quantity, format.Quantity);
    Assert.Equal(image, format.ImageUrl);
  }

  [Fact(DisplayName = "Rejects unknown product format without mutation")]
  public static void Product_UpdateFormat_ThrowsSpecificException_WhenIdIsUnknown() {
    var original = new ProductFormat(
      new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-00000000000a")),
      Quantity.Create(1, "g"),
      Image);
    Product product = CreateProduct([original]);

    Assert.Throws<ProductFormatNotFoundException>(
      () => product.UpdateFormat(
        new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000014")),
        Quantity.Create(2, "g"),
        Image));
    Assert.Same(original, Assert.Single(product.Formats));
  }

  [Fact(DisplayName = "Rejects duplicate formats when rehydrating product")]
  public static void Product_ThrowsSpecificException_WhenFormatsContainDuplicateId() {
    var id = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-00000000000a"));

    Assert.Throws<DuplicateProductFormatException>(
      () => CreateProduct([
        new ProductFormat(id, Quantity.Create(1, "g"), Image),
        new ProductFormat(id, Quantity.Create(2, "g"), Image),
      ]));
  }

  private static Product CreateProduct(
    IEnumerable<ProductFormat>? formats = null
  ) => new(
    new ProductId(Guid.Parse("00000000-0000-7000-8000-000000000001")),
    new ProductName("Milk"),
    new BrandName("Acme"),
    new MarketId(Guid.Parse("00000000-0000-7000-8000-000000000001")),
    formats);
}