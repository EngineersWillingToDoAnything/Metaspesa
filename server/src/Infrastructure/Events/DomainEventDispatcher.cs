using System.Diagnostics.CodeAnalysis;
using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Domain.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Metaspesa.Infrastructure.Events;

internal sealed partial class DomainEventDispatcher(
  InMemoryDomainEventPublisher publisher,
  IServiceScopeFactory scopeFactory,
  ILogger<DomainEventDispatcher> logger
) : BackgroundService {
  protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    await foreach (IDomainEvent domainEvent in publisher.ReadAllAsync(stoppingToken)) {
      await TryDispatchEventAsync(domainEvent, stoppingToken);
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types",
    Justification = "The background boundary logs dispatch failures and continues with later events.")]
  private async Task TryDispatchEventAsync(
    IDomainEvent domainEvent, CancellationToken stoppingToken
  ) {
    try {
      await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
      IEnumerable<IDomainEventHandler> handlers = scope.ServiceProvider
        .GetServices<IDomainEventHandler>();

      foreach (IDomainEventHandler handler in handlers) {
        await TryDispatchAsync(domainEvent, handler, stoppingToken);
      }
    } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
      throw;
    } catch (Exception exception) {
      LogDispatchFailure(exception, domainEvent);
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types",
    Justification = "Handler failures must be logged without affecting checkout or other handlers.")]
  private async Task TryDispatchAsync(
    IDomainEvent domainEvent,
    IDomainEventHandler handler,
    CancellationToken stoppingToken
  ) {
    try {
      if (handler.CanHandle(domainEvent)) {
        await handler.HandleAsync(domainEvent, stoppingToken);
      }
    } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
      throw;
    } catch (Exception exception) {
      LogHandlerFailure(exception, handler.GetType().Name, domainEvent);
    }
  }

  [LoggerMessage(Level = LogLevel.Error,
    Message = "Handler {HandlerType} failed processing {DomainEvent}")]
  private partial void LogHandlerFailure(
    Exception exception, string handlerType, IDomainEvent domainEvent);

  [LoggerMessage(Level = LogLevel.Error,
    Message = "Dispatch failed for {DomainEvent}")]
  private partial void LogDispatchFailure(Exception exception, IDomainEvent domainEvent);
}