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