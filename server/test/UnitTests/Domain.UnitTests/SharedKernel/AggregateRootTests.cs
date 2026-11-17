using System.Collections.Immutable;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.UnitTests.SharedKernel;

public class AggregateRootTests {
  [Fact]
  public void FlushDomainEvents_ReturnsAnEmptySnapshot_WhenNoEventsArePending() {
    // Arrange
    var aggregate = new TestAggregate();

    // Act
    ImmutableArray<IDomainEvent> events = aggregate.FlushDomainEvents();

    // Assert
    Assert.Empty(events);
  }

  [Fact]
  public void FlushDomainEvents_ReturnsThePendingEvent() {
    // Arrange
    var aggregate = new TestAggregate();
    var domainEvent = new TestEvent(1);
    aggregate.Record(domainEvent);

    // Act
    ImmutableArray<IDomainEvent> events = aggregate.FlushDomainEvents();

    // Assert
    Assert.Equal<IDomainEvent>([domainEvent], events);
  }

  [Fact]
  public void FlushDomainEvents_ReturnsMultipleEventsInRecordedOrder() {
    // Arrange
    var aggregate = new TestAggregate();
    var first = new TestEvent(1);
    var second = new TestEvent(2);
    aggregate.Record(first);
    aggregate.Record(second);

    // Act
    ImmutableArray<IDomainEvent> events = aggregate.FlushDomainEvents();

    // Assert
    Assert.Equal<IDomainEvent>([first, second], events);
  }

  [Fact]
  public void FlushDomainEvents_RemovesTheReturnedEventsFromTheAggregate() {
    // Arrange
    var aggregate = new TestAggregate();
    aggregate.Record(new TestEvent(1));
    aggregate.Record(new TestEvent(2));

    // Act
    _ = aggregate.FlushDomainEvents();

    // Assert
    Assert.Empty(aggregate.FlushDomainEvents());
  }

  [Fact]
  public void FlushDomainEvents_ReturnsOnlyEventsRecordedSinceThePreviousFlush() {
    // Arrange
    var aggregate = new TestAggregate();
    aggregate.Record(new TestEvent(1));
    _ = aggregate.FlushDomainEvents();
    var next = new TestEvent(2);
    aggregate.Record(next);

    // Act
    ImmutableArray<IDomainEvent> events = aggregate.FlushDomainEvents();

    // Assert
    Assert.Equal<IDomainEvent>([next], events);
  }

  [Fact]
  public void FlushedSnapshot_IsUnaffectedByLaterRecordingAndFlushing() {
    // Arrange
    var aggregate = new TestAggregate();
    var original = new TestEvent(1);
    aggregate.Record(original);
    ImmutableArray<IDomainEvent> snapshot = aggregate.FlushDomainEvents();

    // Act
    aggregate.Record(new TestEvent(2));
    _ = aggregate.FlushDomainEvents();

    // Assert
    Assert.Equal<IDomainEvent>([original], snapshot);
  }

  [Fact]
  public void Record_RejectsNullEvent() {
    // Arrange
    var aggregate = new TestAggregate();

    // Act
    Exception? failure = Record.Exception(() => aggregate.Record(null!));

    // Assert
    Assert.IsType<ArgumentNullException>(failure);
  }

  private sealed class TestAggregate : AggregateRoot {
    public new void Record(IDomainEvent domainEvent) => base.Record(domainEvent);
  }

  private sealed record TestEvent(int Sequence) : IDomainEvent;
}