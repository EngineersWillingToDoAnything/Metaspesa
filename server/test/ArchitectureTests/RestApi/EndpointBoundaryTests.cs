using System.Reflection;
using Metaspesa.Application.Identity;
using Metaspesa.ArchitectureTests.Dependencies;

namespace Metaspesa.ArchitectureTests.RestApi;

public class EndpointBoundaryTests {
  [Trait("ArchitectureRule", "ARCH-API-002")]
  [Fact]
  public void EndpointAdapters_HaveAtMostOneUseCaseInvocationSite() {
    // Arrange
    var restApi = Assembly.Load("RestApi");
    string[] handlers = [.. typeof(LoginUser).Assembly.GetTypes()
      .Where(type => type.Name == "Handler")
      .Select(DependencyAnalysis.TypeName)
    ];

    // Act
    string[] violations = [.. DependencyAnalysis.MethodUses(restApi)
      .Where(call => call.OwnerType.EndsWith("Endpoint", StringComparison.Ordinal))
      .Where(call => handlers.Contains(call.Target) && call.Method == "Handle")
      .GroupBy(call => call.OwnerType)
      .Where(group => group.Count() > 1)
      .Select(group => group.Key)
    ];

    // Assert
    Assert.Empty(violations);
  }
}