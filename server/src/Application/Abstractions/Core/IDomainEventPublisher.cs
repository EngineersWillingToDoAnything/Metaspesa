using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Application.Abstractions.Core;

public interface IDomainEventPublisher {
  // Completes when events are accepted for dispatch, not when handlers finish.
  Task PublishAsync(
    IReadOnlyCollection<IDomainEvent> domainEvents,
    CancellationToken cancellationToken = default
  );
}