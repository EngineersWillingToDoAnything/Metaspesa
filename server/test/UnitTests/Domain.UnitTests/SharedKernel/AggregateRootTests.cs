using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.UnitTests.SharedKernel;

public class AggregateRootTests {
  [Fact]
  public void NewAggregate_HasNoPendingEvents() {
    var aggregate = new TestAggregate();

    Assert.Empty(aggregate.DomainEvents);
  }

  [Fact]
  public void Record_RecordsEventsInOrder() {
    var aggregate = new TestAggregate();
    var first = new TestEvent(1);
    var second = new TestEvent(2);

    aggregate.Record(first);
    aggregate.Record(second);

    Assert.Equal<IDomainEvent>([first, second], aggregate.DomainEvents);
  }

  [Fact]
  public void DomainEvents_CannotBeModifiedByCaller() {
    var aggregate = new TestAggregate();
    aggregate.Record(new TestEvent(1));
    var events = (ICollection<IDomainEvent>)aggregate.DomainEvents;

    Assert.Throws<NotSupportedException>(() => events.Clear());
  }

  [Fact]
  public void ClearDomainEvents_RemovesPendingEvents() {
    var aggregate = new TestAggregate();
    aggregate.Record(new TestEvent(1));
    aggregate.Record(new TestEvent(2));

    aggregate.ClearDomainEvents();

    Assert.Empty(aggregate.DomainEvents);
  }

  [Fact]
  public void ClearDomainEvents_CanBeCalledWhenEmpty() {
    var aggregate = new TestAggregate();

    aggregate.ClearDomainEvents();
    aggregate.ClearDomainEvents();

    Assert.Empty(aggregate.DomainEvents);
  }

  [Fact]
  public void Record_AfterClear_ContainsOnlyNewEvents() {
    var aggregate = new TestAggregate();
    aggregate.Record(new TestEvent(1));
    aggregate.ClearDomainEvents();
    var next = new TestEvent(2);

    aggregate.Record(next);

    Assert.Equal<IDomainEvent>([next], aggregate.DomainEvents);
  }

  [Fact]
  public void Record_RejectsNullEvent() {
    var aggregate = new TestAggregate();

    Assert.Throws<ArgumentNullException>(() => aggregate.Record(null!));
  }

  private sealed class TestAggregate : AggregateRoot {
    public new void Record(IDomainEvent domainEvent) => base.Record(domainEvent);
  }

  private sealed record TestEvent(int Sequence) : IDomainEvent;
}