using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Application.Markets;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using NSubstitute;

namespace Metaspesa.Application.UnitTests.Markets;

public class CreateBrandTests {
  private readonly IBrandRepository _repository;

  private readonly CreateBrand.Handler _handler;

  public CreateBrandTests() {
    _repository = Substitute.For<IBrandRepository>();

    _handler = new CreateBrand.Handler(_repository);
  }

  [Fact]
  public async Task Handler_ReturnsSavedAggregateId() {
    // Arrange
    Brand? saved = null;
    _repository.SaveAsync(
        Arg.Do<Brand>(aggregate => saved = aggregate),
        Arg.Any<CancellationToken>())
      .Returns(Task.CompletedTask);

    var command = new CreateBrand.Command("Brand");

    // Act
    Guid id = await _handler.Handle(
      command, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(saved!.Id.Value, id);
  }

  [Fact]
  public async Task Handler_RejectsExistingName() {
    // Arrange
    var command = new CreateBrand.Command("Brand");

    _repository
      .ExistsAsync(command.Name, Arg.Any<CancellationToken>())
      .Returns(true);

    // Act
    Task action() => _handler.Handle(command, TestContext.Current.CancellationToken);

    // Assert
    await Assert.ThrowsAsync<BrandAlreadyExistsException>(action);
  }

  [Fact]
  public async Task Handler_DoesNotSaveExistingName() {
    // Arrange
    _repository.ExistsAsync("Brand 1", Arg.Any<CancellationToken>())
      .Returns(true);
    var command = new CreateBrand.Command("Brand 1");

    // Act
    await Record.ExceptionAsync(() => _handler.Handle(command, TestContext.Current.CancellationToken));

    // Assert
    await _repository.DidNotReceive()
      .SaveAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>());
  }
}