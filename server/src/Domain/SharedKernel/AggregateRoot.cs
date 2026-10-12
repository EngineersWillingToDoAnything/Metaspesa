namespace Metaspesa.Domain.SharedKernel;

public abstract class AggregateRoot {
  private readonly List<IDomainEvent> _domainEvents;
  public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

  protected AggregateRoot() {
    _domainEvents = [];
  }

  protected void Record(IDomainEvent domainEvent) {
    ArgumentNullException.ThrowIfNull(domainEvent);
    _domainEvents.Add(domainEvent);
  }

  public void ClearDomainEvents() => _domainEvents.Clear();
}