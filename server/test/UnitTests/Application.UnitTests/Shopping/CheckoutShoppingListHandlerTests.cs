using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;
using Metaspesa.Domain.Shopping.Events;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static Metaspesa.Application.Shopping.CheckoutShoppingList;

namespace Metaspesa.Application.UnitTests.Shopping;

public class CheckoutShoppingListHandlerTests {
  private readonly IShoppingListRepository repository;
  private readonly IClock clock;
  private readonly IDomainEventPublisher publisher;

  private readonly Handler handler;

  public CheckoutShoppingListHandlerTests() {
    repository = Substitute.For<IShoppingListRepository>();
    clock = Substitute.For<IClock>();
    publisher = Substitute.For<IDomainEventPublisher>();

    handler = new Handler(repository, clock, publisher);
  }

  [Fact]
  public async Task Handle_RejectsNullCommand() {
    Task action() => handler.Handle(null!, TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<ArgumentNullException>(action);
  }

  [Fact]
  public async Task Handle_RejectsMissingList() {
    var command = new Command(Guid.CreateVersion7(), Guid.CreateVersion7());

    Task action() => handler.Handle(command, TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<ShoppingListNotFoundException>(action);
  }

  [Fact]
  public async Task Handle_DoesNotSaveMissingList() {
    var command = new Command(Guid.CreateVersion7(), Guid.CreateVersion7());

    Task action() => handler.Handle(command, TestContext.Current.CancellationToken);

    _ = await Record.ExceptionAsync(action);

    await repository.DidNotReceive().SaveAsync(
      Arg.Any<ShoppingList>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_SavesCheckedOutList() {
    var ownerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(ownerId, "Weekly");
    list.AddItems([new AddItemsParams(Guid.CreateVersion7(), 2, true)]);
    var command = new Command(ownerId, list.Id.Value);

    repository.GetAsync(ownerId, list.Id.Value, TestContext.Current.CancellationToken)
      .Returns(list);

    clock.GetCurrentTime().Returns(DateTime.UtcNow);

    await handler.Handle(command, TestContext.Current.CancellationToken);

    await repository.Received(1).SaveAsync(
      Arg.Is<ShoppingList>(savedList => savedList.Id == list.Id),
      TestContext.Current.CancellationToken);
  }

  [Fact]
  public async Task Handle_ReturnsRecordedCheckoutId() {
    var ownerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(ownerId, "Weekly");
    list.AddItems([new AddItemsParams(Guid.CreateVersion7(), 2, true)]);
    var command = new Command(ownerId, list.Id.Value);

    repository.GetAsync(ownerId, list.Id.Value, TestContext.Current.CancellationToken)
      .Returns(list);

    clock.GetCurrentTime().Returns(DateTime.UtcNow);

    IDomainEvent[] publishedEvents = [];
    publisher.When(value => value.PublishAsync(
        Arg.Any<IReadOnlyCollection<IDomainEvent>>(),
        Arg.Any<CancellationToken>()))
      .Do(call => publishedEvents = [.. call.Arg<IReadOnlyCollection<IDomainEvent>>()]);

    Guid result = await handler.Handle(command, TestContext.Current.CancellationToken);

    var domainEvent = (ShoppingListCheckedOut)publishedEvents.Single();
    Assert.Equal(domainEvent.CheckoutId, result);
  }

  [Fact]
  public async Task Handle_PublishesDomainEvents() {
    var ownerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(ownerId, "Weekly");
    list.AddItems([new AddItemsParams(Guid.CreateVersion7(), 2, true)]);
    var command = new Command(ownerId, list.Id.Value);

    repository.GetAsync(ownerId, list.Id.Value, TestContext.Current.CancellationToken)
      .Returns(list);

    clock.GetCurrentTime().Returns(DateTime.UtcNow);

    _ = await handler.Handle(command, TestContext.Current.CancellationToken);

    await publisher.Received(1).PublishAsync(
      Arg.Any<IReadOnlyCollection<IDomainEvent>>(), CancellationToken.None);
  }

  [Fact]
  public async Task Handle_DoesNotPublishWhenSaveFails() {
    var ownerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(ownerId, "Weekly");
    list.AddItems([new AddItemsParams(Guid.CreateVersion7(), 2, true)]);

    repository.GetAsync(ownerId, list.Id.Value, Arg.Any<CancellationToken>())
      .Returns(list);
    clock.GetCurrentTime().Returns(DateTime.UtcNow);

    repository.SaveAsync(list, Arg.Any<CancellationToken>())
      .ThrowsAsync(new InvalidOperationException("Save failed"));

    _ = await Record.ExceptionAsync(() => handler.Handle(
      new Command(ownerId, list.Id.Value), TestContext.Current.CancellationToken));

    await publisher.DidNotReceive().PublishAsync(
      Arg.Any<IReadOnlyCollection<IDomainEvent>>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_ClearsEventsAfterPublication() {
    var ownerId = Guid.CreateVersion7();
    var list = ShoppingList.Create(ownerId, "Weekly");
    list.AddItems([new AddItemsParams(Guid.CreateVersion7(), 2, true)]);

    repository.GetAsync(ownerId, list.Id.Value, Arg.Any<CancellationToken>())
      .Returns(list);

    clock.GetCurrentTime().Returns(DateTime.UtcNow);

    await handler.Handle(new Command(ownerId, list.Id.Value),
      TestContext.Current.CancellationToken);

    Assert.Empty(list.DomainEvents);
  }
}