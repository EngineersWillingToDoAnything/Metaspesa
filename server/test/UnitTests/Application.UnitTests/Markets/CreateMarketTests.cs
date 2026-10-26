using System.Data;
using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Application.Markets;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Metaspesa.Application.UnitTests.Markets;

public class CreateMarketTests {
  private readonly IMarketRepository _repository;

  private readonly CreateMarket.Handler _handler;

  public CreateMarketTests() {
    _repository = Substitute.For<IMarketRepository>();

    _handler = new CreateMarket.Handler(_repository);
  }

  [Fact]
  public async Task Handler_ReturnsSavedAggregateId() {
    // Arrange
    Market? saved = null;
    _repository.SaveAsync(
        Arg.Do<Market>(aggregate => saved = aggregate),
        Arg.Any<CancellationToken>())
      .Returns(Task.CompletedTask);
    var command = new CreateMarket.Command("Market");

    // Act
    Guid id = await _handler.Handle(command, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(saved!.Id.Value, id);
  }

  [Fact]
  public async Task Handler_RejectsExistingName() {
    // Arrange
    var command = new CreateMarket.Command("Market");
    _repository.ExistsAsync(command.Name, Arg.Any<CancellationToken>())
      .Returns(true);

    // Act
    Task action() => _handler.Handle(command, TestContext.Current.CancellationToken);

    // Assert
    await Assert.ThrowsAsync<MarketAlreadyExistsException>(action);
  }

  [Fact]
  public async Task Handler_DoesNotSaveExistingName() {
    // Arrange
    _repository.ExistsAsync("Market", Arg.Any<CancellationToken>()).Returns(true);
    var command = new CreateMarket.Command("Market");

    // Act
    await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    await _repository.DidNotReceive()
      .SaveAsync(Arg.Any<Market>(), Arg.Any<CancellationToken>());
  }
}