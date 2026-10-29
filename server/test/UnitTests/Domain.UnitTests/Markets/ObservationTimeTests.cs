using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.UnitTests.Markets;

public class ObservationTimeTests {
  [Fact]
  public void Create_PreservesUtcTimestamp() {
    var timestamp = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);

    var time = ObservationTime.Create(timestamp);

    Assert.Equal(timestamp, time.Value);
  }

  [Fact]
  public void Create_PreservesUtcKind() {
    var timestamp = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);

    var time = ObservationTime.Create(timestamp);

    Assert.Equal(DateTimeKind.Utc, time.Value.Kind);
  }

  [Theory]
  [InlineData(1L)]
  [InlineData(3155378975999999999L)]
  public void Create_AcceptsUtcBoundary(long ticks) {
    var timestamp = new DateTime(ticks, DateTimeKind.Utc);

    var time = ObservationTime.Create(timestamp);

    Assert.Equal(ticks, time.Value.Ticks);
  }

  [Fact]
  public void Create_RejectsDefaultTimestamp() {
    static void action() => ObservationTime.Create(default);

    Assert.Throws<InvalidPriceSnapshotObservedAtException>(action);
  }

  [Fact]
  public void Create_RejectsUtcMinimum() {
    var timestamp = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

    void action() => ObservationTime.Create(timestamp);

    Assert.Throws<InvalidPriceSnapshotObservedAtException>(action);
  }

  [Theory]
  [InlineData(DateTimeKind.Local)]
  [InlineData(DateTimeKind.Unspecified)]
  public void Create_RejectsNonUtcTimestamp(DateTimeKind kind) {
    var timestamp = DateTime.SpecifyKind(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc), kind);

    void action() => ObservationTime.Create(timestamp);

    Assert.Throws<InvalidPriceSnapshotObservedAtException>(action);
  }

  [Fact]
  public void Constructor_PreservesRawTicks() {
    var timestamp = DateTime.SpecifyKind(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc), DateTimeKind.Unspecified);

    var time = new ObservationTime(timestamp);

    Assert.Equal(timestamp.Ticks, time.Value.Ticks);
  }

  [Theory]
  [InlineData(DateTimeKind.Local)]
  [InlineData(DateTimeKind.Unspecified)]
  [InlineData(DateTimeKind.Utc)]
  public void Constructor_PreservesRawKind(DateTimeKind kind) {
    var timestamp = DateTime.SpecifyKind(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc), kind);

    var time = new ObservationTime(timestamp);

    Assert.Equal(kind, time.Value.Kind);
  }

  [Fact]
  public void Constructor_AcceptsDefaultTimestamp() {
    var time = new ObservationTime(default);

    Assert.Equal(default, time.Value);
  }

  [Theory]
  [InlineData(-1, true)]
  [InlineData(0, false)]
  [InlineData(1, false)]
  public void LessThan_ComparesTimestampValues(int offset, bool expected) {
    var timestamp = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
    var left = ObservationTime.Create(timestamp.AddTicks(offset));
    var right = ObservationTime.Create(timestamp);

    bool actual = left < right;

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(-1, true)]
  [InlineData(0, true)]
  [InlineData(1, false)]
  public void LessThanOrEqual_ComparesTimestampValues(int offset, bool expected) {
    var timestamp = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
    var left = ObservationTime.Create(timestamp.AddTicks(offset));
    var right = ObservationTime.Create(timestamp);

    bool actual = left <= right;

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(-1, false)]
  [InlineData(0, false)]
  [InlineData(1, true)]
  public void GreaterThan_ComparesTimestampValues(int offset, bool expected) {
    var timestamp = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
    var left = ObservationTime.Create(timestamp.AddTicks(offset));
    var right = ObservationTime.Create(timestamp);

    bool actual = left > right;

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(-1, false)]
  [InlineData(0, true)]
  [InlineData(1, true)]
  public void GreaterThanOrEqual_ComparesTimestampValues(int offset, bool expected) {
    var timestamp = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
    var left = ObservationTime.Create(timestamp.AddTicks(offset));
    var right = ObservationTime.Create(timestamp);

    bool actual = left >= right;

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(-1, false)]
  [InlineData(0, true)]
  [InlineData(1, false)]
  public void Equality_ComparesTimestampValues(int offset, bool expected) {
    var timestamp = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
    var left = ObservationTime.Create(timestamp.AddTicks(offset));
    var right = ObservationTime.Create(timestamp);

    bool actual = left == right;

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(-1, true)]
  [InlineData(0, false)]
  [InlineData(1, true)]
  public void Inequality_ComparesTimestampValues(int offset, bool expected) {
    var timestamp = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
    var left = ObservationTime.Create(timestamp.AddTicks(offset));
    var right = ObservationTime.Create(timestamp);

    bool actual = left != right;

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(-1, -1)]
  [InlineData(0, 0)]
  [InlineData(1, 1)]
  public void CompareTo_OrdersTimestampValues(int offset, int expectedSign) {
    var timestamp = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
    var left = ObservationTime.Create(timestamp.AddTicks(offset));
    var right = ObservationTime.Create(timestamp);

    int comparison = left.CompareTo(right);

    Assert.Equal(expectedSign, Math.Sign(comparison));
  }

  [Fact]
  public void GetHashCode_MatchesForEqualTimes() {
    var timestamp = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
    var first = ObservationTime.Create(timestamp);
    var second = new ObservationTime(timestamp);

    Assert.Equal(first.GetHashCode(), second.GetHashCode());
  }
}