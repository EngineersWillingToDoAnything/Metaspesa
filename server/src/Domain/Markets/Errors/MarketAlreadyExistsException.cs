namespace Metaspesa.Domain.Markets.Errors;

public sealed class MarketAlreadyExistsException : MarketDomainException {
  public MarketAlreadyExistsException() : this("Market name already exists.") { }
  public MarketAlreadyExistsException(string message) : base("Market.AlreadyExists", message) { }
  public MarketAlreadyExistsException(string message, Exception innerException)
    : base("Market.AlreadyExists", message, innerException) { }
  public MarketAlreadyExistsException(MarketName name) : this($"Market '{name}' already exists.") { }
  public MarketAlreadyExistsException(MarketName name, Exception innerException)
    : this($"Market '{name}' already exists.", innerException) { }
}