using Metaspesa.Domain.Identity.Errors;

namespace Metaspesa.Domain.Identity;

public readonly record struct UserId {
  public Guid Value { get; }

  public UserId(Guid value) {
    Value = value;
  }

  public static UserId Create(Guid value) {
    if (value == Guid.Empty) {
      throw new InvalidUserIdException();
    }

    return new UserId(value);
  }

  public override string ToString() => Value.ToString();
}