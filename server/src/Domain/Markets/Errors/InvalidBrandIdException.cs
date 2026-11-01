namespace Metaspesa.Domain.Markets.Errors;

public class InvalidBrandIdException : MarketDomainException {
  public InvalidBrandIdException() { }
  public InvalidBrandIdException(Guid value)
    : base("Brand.Id.Invalid", $"Brand id '{value}' must not be empty.") { }
  public InvalidBrandIdException(string message) : base(message) { }
  public InvalidBrandIdException(string message, Exception innerException)
    : base(message, innerException) { }
}