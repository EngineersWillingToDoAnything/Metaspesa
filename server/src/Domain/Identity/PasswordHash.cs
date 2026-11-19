using Metaspesa.Domain.Identity.Errors;

namespace Metaspesa.Domain.Identity;

public readonly record struct PasswordHash {
  public string Value { get; }

  public PasswordHash(string value) {
    Value = value;
  }

  public static PasswordHash Create(string value) {
    if (string.IsNullOrWhiteSpace(value)) {
      throw new InvalidPasswordHashException();
    }

    return new PasswordHash(value);
  }

  public override string ToString() => Value;
}