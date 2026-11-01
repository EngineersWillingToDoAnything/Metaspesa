namespace Metaspesa.Domain.Markets.Errors;

public sealed class UnknownMarketException : MarketDomainException {
  public UnknownMarketException() : this("Market does not exist.") { }
  public UnknownMarketException(string message) : base("Ingestion.UnknownMarket", message) { }
  public UnknownMarketException(string message, Exception innerException)
    : base("Ingestion.UnknownMarket", message, innerException) { }
  public UnknownMarketException(MarketName marketName) : this($"Market '{marketName}' does not exist.") {
    MarketName = marketName;
  }

  public MarketName MarketName { get; }
}