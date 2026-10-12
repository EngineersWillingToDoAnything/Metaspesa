using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Application.Abstractions.Core;

public interface IDomainEventHandler {
  bool CanHandle(IDomainEvent domainEvent);
  Task HandleAsync(
    IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}