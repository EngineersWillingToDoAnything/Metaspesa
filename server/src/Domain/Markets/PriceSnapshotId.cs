using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.Markets;

public readonly record struct PriceSnapshotId {
  public Guid Value { get; }

  public PriceSnapshotId(Guid value) {
    Value = value;
  }

  public static PriceSnapshotId Create(Guid value) {
    if (value == Guid.Empty) {
      throw new InvalidPriceSnapshotIdException(value);
    }

    return new PriceSnapshotId(value);
  }

  public override string ToString() => Value.ToString();
}