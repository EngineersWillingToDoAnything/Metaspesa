using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Identity;

namespace Metaspesa.ArchitectureTests.Application;

public class HandlerContractTests {
  [Trait("ArchitectureRule", "ARCH-APP-001")]
  [Fact]
  public void EachHandler_ImplementsExactlyOneCommandOrQueryContract() {
    // Arrange
    Type[] handlers = [.. typeof(LoginUser).Assembly.GetTypes()
      .Where(type => !type.IsAbstract && type.Name == "Handler")
    ];
    Type[] contractDefinitions = [
      typeof(ICommandHandler<>),
      typeof(ICommandHandler<,>),
      typeof(IQueryHandler<>),
      typeof(IQueryHandler<,>)
    ];

    // Act
    Type[] violations = [.. handlers.Where(handler => handler.GetInterfaces().Count(contract =>
      contract.IsGenericType && contractDefinitions.Contains(contract.GetGenericTypeDefinition())) != 1)
    ];

    // Assert
    Assert.Empty(violations);
  }
}