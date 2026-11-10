using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.Shopping;

public readonly record struct PurchaseTime(
  DateTime Value
) : IComparable<PurchaseTime> {
  public static PurchaseTime Create(DateTime value) {
    if (value == default || value.Kind != DateTimeKind.Utc) {
      throw new InvalidPurchasedAtException(value);
    }

    return new PurchaseTime(value);
  }

  public int CompareTo(PurchaseTime other) => Value.CompareTo(other.Value);

  public static bool operator <(PurchaseTime left, PurchaseTime right) => left.CompareTo(right) < 0;
  public static bool operator >(PurchaseTime left, PurchaseTime right) => left.CompareTo(right) > 0;
  public static bool operator <=(PurchaseTime left, PurchaseTime right) => left.CompareTo(right) <= 0;
  public static bool operator >=(PurchaseTime left, PurchaseTime right) => left.CompareTo(right) >= 0;
}