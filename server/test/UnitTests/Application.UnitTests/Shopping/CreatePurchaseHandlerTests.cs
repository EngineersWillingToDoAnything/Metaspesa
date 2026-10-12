using Metaspesa.Application.Abstractions.Purchasing;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;
using NSubstitute;
using static Metaspesa.Application.Shopping.CreatePurchase;

namespace Metaspesa.Application.UnitTests.Shopping;

public class CreatePurchaseHandlerTests {
  private readonly IPurchaseRepository repository;
  private readonly IPurchasePriceSnapshotReader reader;

  private readonly Handler handler;

  public CreatePurchaseHandlerTests() {
    repository = Substitute.For<IPurchaseRepository>();
    reader = Substitute.For<IPurchasePriceSnapshotReader>();

    handler = new Handler(reader, repository);
  }

  [Fact]
  public async Task Handle_RejectsNullCommand() {
    Task action() => handler.Handle(null!, TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<ArgumentNullException>(action);
  }

  [Fact]
  public async Task Handle_ReturnsExistingPurchaseId() {
    var purchaseId = Guid.CreateVersion7();
    var command = new Command(
      purchaseId,
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      DateTime.UtcNow,
      [new CommandItem(Guid.CreateVersion7(), 2)]);

    var createParams = new CreatePurchaseParams(
      purchaseId,
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      DateTime.UtcNow,
      [new CreatePurchaseItem(Guid.CreateVersion7(), 2)]);

    var purchase = Purchase.Create(createParams);
    repository.GetAsync(purchaseId, TestContext.Current.CancellationToken)
      .Returns(purchase);

    Guid result = await handler.Handle(
      command, TestContext.Current.CancellationToken);

    Assert.Equal(purchaseId, result);
  }

  [Fact]
  public async Task Handle_ResolvesSnapshotsAtCheckoutTime() {
    var formatId = Guid.CreateVersion7();
    DateTime purchasedAt = DateTime.UtcNow;
    var command = new Command(
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      purchasedAt,
      [new CommandItem(formatId, 2)]);

    reader.GetSnapshotsIdAsync(
        Arg.Any<IReadOnlyCollection<Guid>>(),
        purchasedAt,
        TestContext.Current.CancellationToken)
      .Returns(new Dictionary<Guid, Guid> { [formatId] = Guid.CreateVersion7() });

    await handler.Handle(command, TestContext.Current.CancellationToken);

    await reader.Received(1).GetSnapshotsIdAsync(
      Arg.Is<IReadOnlyCollection<Guid>>(values =>
        values.SequenceEqual(new[] { formatId })),
      purchasedAt,
      TestContext.Current.CancellationToken);
  }

  [Fact]
  public async Task Handle_RejectsMissingHistoricalSnapshot() {
    DateTime purchasedAt = DateTime.UtcNow;
    var command = new Command(
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      purchasedAt,
      [new CommandItem(Guid.CreateVersion7(), 2)]);

    reader.GetSnapshotsIdAsync(
        Arg.Any<IReadOnlyCollection<Guid>>(),
        purchasedAt,
        TestContext.Current.CancellationToken)
      .Returns(new Dictionary<Guid, Guid>());

    Task action() => handler.Handle(command, TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<PurchasePriceSnapshotNotFoundException>(action);
  }

  [Fact]
  public async Task Handle_DoesNotSave_WhenSnapshotIsMissing() {
    DateTime purchasedAt = DateTime.UtcNow;
    var command = new Command(
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      purchasedAt,
      [new CommandItem(Guid.CreateVersion7(), 2)]);

    reader.GetSnapshotsIdAsync(
        Arg.Any<IReadOnlyCollection<Guid>>(),
        purchasedAt,
        TestContext.Current.CancellationToken)
      .Returns(new Dictionary<Guid, Guid>());

    Task action() => handler.Handle(command, TestContext.Current.CancellationToken);
    _ = await Record.ExceptionAsync(action);

    await repository.DidNotReceive().SaveAsync(
      Arg.Any<Purchase>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_SavesPurchase() {
    var purchaseId = Guid.CreateVersion7();
    var formatId = Guid.CreateVersion7();
    var snapshotId = Guid.CreateVersion7();
    DateTime purchasedAt = DateTime.UtcNow;
    var command = new Command(
      purchaseId,
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      purchasedAt,
      [new CommandItem(formatId, 2)]);

    reader.GetSnapshotsIdAsync(
        Arg.Any<IReadOnlyCollection<Guid>>(),
        purchasedAt,
        TestContext.Current.CancellationToken)
      .Returns(new Dictionary<Guid, Guid> { [formatId] = snapshotId });

    await handler.Handle(command, TestContext.Current.CancellationToken);

    await repository.Received(1).SaveAsync(
      Arg.Is<Purchase>(purchase => purchase.Id.Value == purchaseId),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_ReturnsPurchaseId() {
    var purchaseId = Guid.CreateVersion7();
    var formatId = Guid.CreateVersion7();
    DateTime purchasedAt = DateTime.UtcNow;
    var command = new Command(
      purchaseId,
      Guid.CreateVersion7(),
      Guid.CreateVersion7(),
      purchasedAt,
      [new CommandItem(formatId, 2)]);

    reader.GetSnapshotsIdAsync(
        Arg.Any<IReadOnlyCollection<Guid>>(),
        purchasedAt,
        TestContext.Current.CancellationToken)
      .Returns(new Dictionary<Guid, Guid> { [formatId] = Guid.CreateVersion7() });

    Guid result = await handler.Handle(
      command, TestContext.Current.CancellationToken);

    Assert.Equal(purchaseId, result);
  }
}