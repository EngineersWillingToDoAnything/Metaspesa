using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.RestApi.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Metaspesa.RestApi.UnitTests.Errors;

public class RestExceptionHandlerTests {
  private readonly CapturingProblems _problems;
  private readonly RestExceptionHandler _handler;

  public RestExceptionHandlerTests() {
    _problems = new CapturingProblems();

    _handler = new RestExceptionHandler(
      _problems,
      Substitute.For<ILogger<RestExceptionHandler>>());
  }

  [Fact]
  public async Task UnknownBrand_ReturnsExpectedStatus() {
    // Arrange
    var http = new DefaultHttpContext();
    var failure = new UnknownProductBrandException(
      new MarketName("Market"), new ProductName("Milk"), new BrandName("Brand"));

    // Act
    await _handler.TryHandleAsync(http, failure, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(StatusCodes.Status422UnprocessableEntity, http.Response.StatusCode);
  }

  [Theory]
  [InlineData("code", "Ingestion.UnknownBrand")]
  [InlineData("marketName", "Market")]
  [InlineData("productName", "Milk")]
  [InlineData("brandName", "Brand")]
  public async Task UnknownBrand_ReturnsExpectedContextField(
    string field, string expected
  ) {
    // Arrange
    var http = new DefaultHttpContext();
    var failure = new UnknownProductBrandException(
      new MarketName("Market"), new ProductName("Milk"), new BrandName("Brand"));

    // Act
    await _handler.TryHandleAsync(http, failure, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(expected, _problems.Written!.ProblemDetails.Extensions[field]);
  }

  [Fact]
  public async Task UnknownMarket_ReturnsExpectedStatus() {
    // Arrange
    var http = new DefaultHttpContext();
    var failure = new UnknownMarketException(new MarketName("Market"));

    // Act
    await _handler.TryHandleAsync(http, failure, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(StatusCodes.Status409Conflict, http.Response.StatusCode);
  }

  [Theory]
  [InlineData("code", "Ingestion.UnknownMarket")]
  [InlineData("marketName", "Market")]
  public async Task UnknownMarket_ReturnsExpectedContextField(
    string field, string expected
  ) {
    // Arrange
    var http = new DefaultHttpContext();
    var failure = new UnknownMarketException(new MarketName("Market"));

    // Act
    await _handler.TryHandleAsync(http, failure, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(expected, _problems.Written!.ProblemDetails.Extensions[field]);
  }

  [Fact]
  public async Task ConflictingObservation_ReturnsBadRequest() {
    var http = new DefaultHttpContext();
    var failure = new ConflictingPriceObservationException(
      new ProductFormatId(Guid.CreateVersion7()),
      ObservationDate.Create(new DateOnly(2026, 7, 28)));

    await _handler.TryHandleAsync(http, failure, TestContext.Current.CancellationToken);

    Assert.Equal(StatusCodes.Status400BadRequest, http.Response.StatusCode);
  }

  [Fact]
  public async Task ConflictingObservation_PreservesDomainErrorCode() {
    var http = new DefaultHttpContext();
    var failure = new ConflictingPriceObservationException(
      new ProductFormatId(Guid.CreateVersion7()),
      ObservationDate.Create(new DateOnly(2026, 7, 28)));

    await _handler.TryHandleAsync(http, failure, TestContext.Current.CancellationToken);

    Assert.Equal(failure.Code, _problems.Written!.ProblemDetails.Extensions["code"]);
  }

  [Fact]
  public async Task InfrastructureFailure_ReturnsServerError() {
    var http = new DefaultHttpContext();
    var failure = new TimeoutException("Database unavailable.");

    await _handler.TryHandleAsync(http, failure, TestContext.Current.CancellationToken);

    Assert.Equal(StatusCodes.Status500InternalServerError, http.Response.StatusCode);
  }

  [Fact]
  public async Task InfrastructureFailure_ReturnsServerErrorCode() {
    var http = new DefaultHttpContext();
    var failure = new TimeoutException("Database unavailable.");

    await _handler.TryHandleAsync(http, failure, TestContext.Current.CancellationToken);

    Assert.Equal("Server.InternalError", _problems.Written!.ProblemDetails.Extensions["code"]);
  }

  private sealed class CapturingProblems : IProblemDetailsService {
    public ProblemDetailsContext? Written { get; private set; }

    public ValueTask WriteAsync(ProblemDetailsContext context) {
      Written = context;
      return ValueTask.CompletedTask;
    }

    public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context) {
      Written = context;
      return ValueTask.FromResult(true);
    }
  }
}