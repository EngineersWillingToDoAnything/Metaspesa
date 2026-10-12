using Metaspesa.Domain.SharedKernel.Errors;

namespace Metaspesa.Domain.Shopping.Errors;

public abstract class PurchaseDomainException : DomainException {
  protected PurchaseDomainException() { }
  protected PurchaseDomainException(string message) : base(message) { }
  protected PurchaseDomainException(string message, Exception innerException)
    : base(message, innerException) { }
  protected PurchaseDomainException(string code, string message) : base(message) {
    Code = code;
  }
  protected PurchaseDomainException(
    string code, string message, Exception innerException
  ) : base(message, innerException) {
    Code = code;
  }

  public string Code { get; } = "Purchase.Error";
}