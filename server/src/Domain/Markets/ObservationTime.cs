using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.Markets;

public readonly record struct ObservationTime(
  DateTime Value
) : IComparable<ObservationTime> {
  public static ObservationTime Create(DateTime value) {
    if (value == default || value.Kind != DateTimeKind.Utc) {
      throw new InvalidPriceSnapshotObservedAtException(value);
    }

    return new ObservationTime(value);
  }

  public int CompareTo(ObservationTime other) => Value.CompareTo(other.Value);

  public static bool operator <(ObservationTime left, ObservationTime right) => left.CompareTo(right) < 0;
  public static bool operator >(ObservationTime left, ObservationTime right) => left.CompareTo(right) > 0;
  public static bool operator <=(ObservationTime left, ObservationTime right) => left.CompareTo(right) <= 0;
  public static bool operator >=(ObservationTime left, ObservationTime right) => left.CompareTo(right) >= 0;
}