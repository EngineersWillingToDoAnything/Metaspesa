namespace Metaspesa.Domain.Markets.Errors;

public sealed class BrandAlreadyExistsException : MarketDomainException {
  public BrandAlreadyExistsException() : this("Brand name already exists.") { }
  public BrandAlreadyExistsException(string message) : base("Brand.AlreadyExists", message) { }
  public BrandAlreadyExistsException(string message, Exception innerException)
    : base("Brand.AlreadyExists", message, innerException) { }
  public BrandAlreadyExistsException(BrandName name) : this($"Brand '{name}' already exists.") { }
  public BrandAlreadyExistsException(BrandName name, Exception innerException)
    : this($"Brand '{name}' already exists.", innerException) { }
}