using System.Collections.Concurrent;
using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Application.Shopping;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping;
using Metaspesa.Infrastructure.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Metaspesa.Infrastructure.UnitTests.Events;

public class DomainEventDispatcherTests {
  private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

  [Fact]
  public async Task Checkout_ReturnsWhileEventHandlerIsBlocked() {
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var consumer = new DelegateHandler(async (_, token) => {
      entered.SetResult();
      await release.Task.WaitAsync(token);
    });
    await using ServiceProvider provider = new ServiceCollection()
      .AddScoped<IDomainEventHandler>(_ => consumer)
      .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    var publisher = new InMemoryDomainEventPublisher();
    using var dispatcher = new DomainEventDispatcher(publisher,
      provider.GetRequiredService<IServiceScopeFactory>(),
      Substitute.For<ILogger<DomainEventDispatcher>>());
    var ownerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(ownerId, "Weekly");
    list.AddItems([new AddItemsParams(Guid.CreateVersion7(), 2, true)]);
    IShoppingListRepository repository = Substitute.For<IShoppingListRepository>();
    repository.GetAsync(ownerId, list.Id.Value, Arg.Any<CancellationToken>()).Returns(list);
    IClock clock = Substitute.For<IClock>();
    clock.GetCurrentTime().Returns(new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc));
    var checkout = new CheckoutShoppingList.Handler(repository, clock, publisher);
    await dispatcher.StartAsync(TestContext.Current.CancellationToken);
    try {
      Guid checkoutId = await checkout.Handle(new(ownerId, list.Id.Value),
        TestContext.Current.CancellationToken).WaitAsync(Timeout,
          TestContext.Current.CancellationToken);
      await entered.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

      Assert.NotEqual(Guid.Empty, checkoutId);
    } finally {
      release.TrySetResult();
      await dispatcher.StopAsync(TestContext.Current.CancellationToken);
    }
  }

  [Fact]
  public async Task Dispatch_LogsHandlerFailure() {
    var exception = new InvalidOperationException("Purchase failed");
    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    ILogger<DomainEventDispatcher> logger = Substitute.For<ILogger<DomainEventDispatcher>>();
    logger.IsEnabled(LogLevel.Error).Returns(true);
    await using ServiceProvider provider = new ServiceCollection()
      .AddScoped<IDomainEventHandler>(_ => new DelegateHandler((_, _) =>
        Task.FromException(exception)))
      .AddScoped<IDomainEventHandler>(_ => new DelegateHandler((_, _) => {
        completed.SetResult();
        return Task.CompletedTask;
      }))
      .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    var publisher = new InMemoryDomainEventPublisher();
    using var dispatcher = new DomainEventDispatcher(publisher,
      provider.GetRequiredService<IServiceScopeFactory>(), logger);
    await dispatcher.StartAsync(TestContext.Current.CancellationToken);
    try {
      await publisher.PublishAsync([new TestEvent(1)], TestContext.Current.CancellationToken);
      await completed.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

      Assert.Contains(logger.ReceivedCalls(), call =>
        call.GetMethodInfo().Name == nameof(ILogger.Log) &&
        Equals(call.GetArguments()[0], LogLevel.Error) &&
        ReferenceEquals(call.GetArguments()[3], exception));
    } finally {
      await dispatcher.StopAsync(TestContext.Current.CancellationToken);
    }
  }

  [Fact]
  public async Task Dispatch_ContinuesWithLaterEventsAfterFailure() {
    var completed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    await using ServiceProvider provider = new ServiceCollection()
      .AddScoped<IDomainEventHandler>(_ => new DelegateHandler((domainEvent, _) => {
        var item = (TestEvent)domainEvent;
        if (item.Sequence == 1) {
          throw new InvalidOperationException("First event failed");
        }
        completed.SetResult(item.Sequence);
        return Task.CompletedTask;
      }))
      .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    var publisher = new InMemoryDomainEventPublisher();
    using var dispatcher = new DomainEventDispatcher(publisher,
      provider.GetRequiredService<IServiceScopeFactory>(),
      Substitute.For<ILogger<DomainEventDispatcher>>());
    await dispatcher.StartAsync(TestContext.Current.CancellationToken);
    try {
      await publisher.PublishAsync([new TestEvent(1), new TestEvent(2)],
        TestContext.Current.CancellationToken);

      Assert.Equal(2, await completed.Task.WaitAsync(Timeout,
        TestContext.Current.CancellationToken));
    } finally {
      await dispatcher.StopAsync(TestContext.Current.CancellationToken);
    }
  }

  [Fact]
  public async Task Dispatch_CreatesAndDisposesSeparateScopePerEvent() {
    var disposed = new ConcurrentQueue<Guid>();
    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    await using ServiceProvider provider = new ServiceCollection()
      .AddScoped<IDomainEventHandler>(_ => new ScopedHandler(disposed, completed))
      .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    var publisher = new InMemoryDomainEventPublisher();
    using var dispatcher = new DomainEventDispatcher(publisher,
      provider.GetRequiredService<IServiceScopeFactory>(),
      Substitute.For<ILogger<DomainEventDispatcher>>());
    await dispatcher.StartAsync(TestContext.Current.CancellationToken);
    try {
      await publisher.PublishAsync([new TestEvent(1), new TestEvent(2)],
        TestContext.Current.CancellationToken);
      await completed.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

      Assert.Equal(2, disposed.Distinct().Count());
    } finally {
      await dispatcher.StopAsync(TestContext.Current.CancellationToken);
    }
  }

  [Fact]
  public async Task Dispatch_SkipsNonMatchingHandlers() {
    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    IDomainEventHandler unmatched = Substitute.For<IDomainEventHandler>();
    await using ServiceProvider provider = new ServiceCollection()
      .AddScoped(_ => unmatched)
      .AddScoped<IDomainEventHandler>(_ => new DelegateHandler((_, _) => {
        completed.SetResult();
        return Task.CompletedTask;
      }))
      .BuildServiceProvider();
    var publisher = new InMemoryDomainEventPublisher();
    using var dispatcher = new DomainEventDispatcher(publisher,
      provider.GetRequiredService<IServiceScopeFactory>(),
      Substitute.For<ILogger<DomainEventDispatcher>>());
    await dispatcher.StartAsync(TestContext.Current.CancellationToken);
    try {
      await publisher.PublishAsync([new TestEvent(1)], TestContext.Current.CancellationToken);
      await completed.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

      await unmatched.DidNotReceive().HandleAsync(
        Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
    } finally {
      await dispatcher.StopAsync(TestContext.Current.CancellationToken);
    }
  }

  private sealed record TestEvent(int Sequence) : IDomainEvent;

  private sealed class DelegateHandler(
    Func<IDomainEvent, CancellationToken, Task> handle
  ) : IDomainEventHandler {
    public bool CanHandle(IDomainEvent domainEvent) => true;
    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default) =>
      handle(domainEvent, cancellationToken);
  }

  private sealed class ScopedHandler(
    ConcurrentQueue<Guid> disposed, TaskCompletionSource completed
  ) : IDomainEventHandler, IAsyncDisposable {
    private readonly Guid _id = Guid.CreateVersion7();
    public bool CanHandle(IDomainEvent domainEvent) => true;
    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
    public ValueTask DisposeAsync() {
      disposed.Enqueue(_id);
      if (disposed.Count == 2) {
        completed.SetResult();
      }
      return ValueTask.CompletedTask;
    }
  }
}