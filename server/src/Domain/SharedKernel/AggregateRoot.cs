using System.Collections.Immutable;

namespace Metaspesa.Domain.SharedKernel;

public abstract class AggregateRoot {
  private readonly List<IDomainEvent> _domainEvents;

  protected AggregateRoot() {
    _domainEvents = [];
  }

  protected void Record(IDomainEvent domainEvent) {
    ArgumentNullException.ThrowIfNull(domainEvent);
    _domainEvents.Add(domainEvent);
  }

  public ImmutableArray<IDomainEvent> FlushDomainEvents() {
    ImmutableArray<IDomainEvent> events = [.. _domainEvents];
    _domainEvents.Clear();
    return events;
  }
}