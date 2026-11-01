using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.Markets;

public readonly record struct ObservationDate(
  DateOnly Value
) : IComparable<ObservationDate> {
  public static ObservationDate Create(DateOnly value) {
    if (value == default) {
      throw new InvalidPriceSnapshotObservedOnException(value);
    }

    return new ObservationDate(value);
  }

  public int CompareTo(ObservationDate other) => Value.CompareTo(other.Value);

  public static bool operator <(ObservationDate left, ObservationDate right) => left.CompareTo(right) < 0;
  public static bool operator >(ObservationDate left, ObservationDate right) => left.CompareTo(right) > 0;
  public static bool operator <=(ObservationDate left, ObservationDate right) => left.CompareTo(right) <= 0;
  public static bool operator >=(ObservationDate left, ObservationDate right) => left.CompareTo(right) >= 0;
}