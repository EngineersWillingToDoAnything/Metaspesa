using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.UnitTests.Markets;

public class ObservationDateTests {
  [Fact]
  public void Create_PreservesDate() {
    var date = new DateOnly(2026, 7, 28);

    var observationDate = ObservationDate.Create(date);

    Assert.Equal(date, observationDate.Value);
  }

  [Fact]
  public void Create_RejectsDefaultDate() {
    static void action() => ObservationDate.Create(default);

    Assert.Throws<InvalidPriceSnapshotObservedOnException>(action);
  }

  [Fact]
  public void Create_AcceptsFirstNonDefaultDate() {
    DateOnly date = DateOnly.MinValue.AddDays(1);

    var observationDate = ObservationDate.Create(date);

    Assert.Equal(date, observationDate.Value);
  }

  [Fact]
  public void Create_AcceptsMaximumDate() {
    DateOnly date = DateOnly.MaxValue;

    var observationDate = ObservationDate.Create(date);

    Assert.Equal(date, observationDate.Value);
  }

  [Fact]
  public void Constructor_PreservesRawDate() {
    var date = new DateOnly(2026, 7, 28);

    var observationDate = new ObservationDate(date);

    Assert.Equal(date, observationDate.Value);
  }

  [Fact]
  public void Constructor_AcceptsDefaultDate() {
    var observationDate = new ObservationDate(default);

    Assert.Equal(default, observationDate.Value);
  }

  [Fact]
  public void LessThan_ReturnsTrue_ForEarlierDate() {
    var date = new DateOnly(2026, 7, 28);
    var left = ObservationDate.Create(date.AddDays(-1));
    var right = ObservationDate.Create(date);

    bool actual = left < right;

    Assert.True(actual);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  public void LessThan_ReturnsFalse_ForEqualOrLaterDate(int daysLater) {
    var date = new DateOnly(2026, 7, 28);
    var left = ObservationDate.Create(date.AddDays(daysLater));
    var right = ObservationDate.Create(date);

    bool actual = left < right;

    Assert.False(actual);
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(0)]
  public void LessThanOrEqual_ReturnsTrue_ForEarlierOrEqualDate(int daysLater) {
    var date = new DateOnly(2026, 7, 28);
    var left = ObservationDate.Create(date.AddDays(daysLater));
    var right = ObservationDate.Create(date);

    bool actual = left <= right;

    Assert.True(actual);
  }

  [Fact]
  public void LessThanOrEqual_ReturnsFalse_ForLaterDate() {
    var date = new DateOnly(2026, 7, 28);
    var left = ObservationDate.Create(date.AddDays(1));
    var right = ObservationDate.Create(date);

    bool actual = left <= right;

    Assert.False(actual);
  }

  [Fact]
  public void GreaterThan_ReturnsTrue_ForLaterDate() {
    var date = new DateOnly(2026, 7, 28);
    var left = ObservationDate.Create(date.AddDays(1));
    var right = ObservationDate.Create(date);

    bool actual = left > right;

    Assert.True(actual);
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(0)]
  public void GreaterThan_ReturnsFalse_ForEarlierOrEqualDate(int daysLater) {
    var date = new DateOnly(2026, 7, 28);
    var left = ObservationDate.Create(date.AddDays(daysLater));
    var right = ObservationDate.Create(date);

    bool actual = left > right;

    Assert.False(actual);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  public void GreaterThanOrEqual_ReturnsTrue_ForEqualOrLaterDate(int daysLater) {
    var date = new DateOnly(2026, 7, 28);
    var left = ObservationDate.Create(date.AddDays(daysLater));
    var right = ObservationDate.Create(date);

    bool actual = left >= right;

    Assert.True(actual);
  }

  [Fact]
  public void GreaterThanOrEqual_ReturnsFalse_ForEarlierDate() {
    var date = new DateOnly(2026, 7, 28);
    var left = ObservationDate.Create(date.AddDays(-1));
    var right = ObservationDate.Create(date);

    bool actual = left >= right;

    Assert.False(actual);
  }

  [Fact]
  public void Equality_ReturnsTrue_ForSameDate() {
    var date = new DateOnly(2026, 7, 28);
    var left = ObservationDate.Create(date.AddDays(0));
    var right = ObservationDate.Create(date);

    bool actual = left == right;

    Assert.True(actual);
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(1)]
  public void Equality_ReturnsFalse_ForDifferentDates(int daysLater) {
    var date = new DateOnly(2026, 7, 28);
    var left = ObservationDate.Create(date.AddDays(daysLater));
    var right = ObservationDate.Create(date);

    bool actual = left == right;

    Assert.False(actual);
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(1)]
  public void Inequality_ReturnsTrue_ForDifferentDates(int daysLater) {
    var date = new DateOnly(2026, 7, 28);
    var left = ObservationDate.Create(date.AddDays(daysLater));
    var right = ObservationDate.Create(date);

    bool actual = left != right;

    Assert.True(actual);
  }

  [Fact]
  public void Inequality_ReturnsFalse_ForSameDate() {
    var date = new DateOnly(2026, 7, 28);
    var left = ObservationDate.Create(date.AddDays(0));
    var right = ObservationDate.Create(date);

    bool actual = left != right;

    Assert.False(actual);
  }

  [Theory]
  [InlineData(-1, -1)]
  [InlineData(0, 0)]
  [InlineData(1, 1)]
  public void CompareTo_OrdersDateValues(int daysLater, int expectedSign) {
    var date = new DateOnly(2026, 7, 28);
    var left = ObservationDate.Create(date.AddDays(daysLater));
    var right = ObservationDate.Create(date);

    int comparison = left.CompareTo(right);

    Assert.Equal(expectedSign, Math.Sign(comparison));
  }

  [Fact]
  public void GetHashCode_MatchesForEqualDates() {
    var date = new DateOnly(2026, 7, 28);
    var first = ObservationDate.Create(date);
    var second = ObservationDate.Create(date);

    Assert.Equal(first.GetHashCode(), second.GetHashCode());
  }
}