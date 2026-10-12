using System.Threading.Channels;
using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Infrastructure.Events;

internal sealed class InMemoryDomainEventPublisher : IDomainEventPublisher {
  private readonly Channel<IDomainEvent> _events =
    Channel.CreateUnbounded<IDomainEvent>(new UnboundedChannelOptions {
      SingleReader = true,
      AllowSynchronousContinuations = false,
    });

  public async Task PublishAsync(
    IReadOnlyCollection<IDomainEvent> domainEvents,
    CancellationToken cancellationToken = default
  ) {
    ArgumentNullException.ThrowIfNull(domainEvents);

    foreach (IDomainEvent domainEvent in domainEvents) {
      await _events.Writer.WriteAsync(domainEvent, cancellationToken);
    }
  }

  internal IAsyncEnumerable<IDomainEvent> ReadAllAsync(
    CancellationToken cancellationToken
  ) => _events.Reader.ReadAllAsync(cancellationToken);
}