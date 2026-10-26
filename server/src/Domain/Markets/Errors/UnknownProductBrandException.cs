namespace Metaspesa.Domain.Markets.Errors;

public sealed class UnknownProductBrandException : MarketDomainException {
  public UnknownProductBrandException() : this("Product references an unknown Brand.") { }
  public UnknownProductBrandException(string message) : base("Ingestion.UnknownBrand", message) { }
  public UnknownProductBrandException(string message, Exception innerException)
    : base("Ingestion.UnknownBrand", message, innerException) { }
  public UnknownProductBrandException(MarketName marketName, ProductName productName, BrandName brandName)
    : this($"Product '{productName}' in market '{marketName}' references unknown brand '{brandName}'.") {
    MarketName = marketName;
    ProductName = productName;
    BrandName = brandName;
  }

  public MarketName MarketName { get; }
  public ProductName ProductName { get; }
  public BrandName BrandName { get; }
}