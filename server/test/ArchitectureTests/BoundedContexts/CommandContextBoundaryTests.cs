using System.Reflection;
using Metaspesa.Application.Identity;
using Metaspesa.ArchitectureTests.Dependencies;

namespace Metaspesa.ArchitectureTests.BoundedContexts;

public class CommandContextBoundaryTests {
  [Trait("ArchitectureRule", "ARCH-BC-004")]
  [Theory]
  [InlineData("Metaspesa.Application.Identity", "Metaspesa.Application.Abstractions.Users.")]
  [InlineData("Metaspesa.Application.Markets", "Metaspesa.Application.Abstractions.Markets.")]
  [InlineData("Metaspesa.Application.Shopping", "Metaspesa.Application.Abstractions.Shopping.")]
  public void ContextWrites_UseOnlyTheirOwnRepositoryContracts(
    string contextNamespace, string repositoryNamespace
  ) {
    // Arrange
    Assembly application = typeof(LoginUser).Assembly;
    string[] repositories = [.. application.GetTypes()
      .Where(type => type.IsInterface && type.Name.EndsWith("Repository", StringComparison.Ordinal))
      .Select(DependencyAnalysis.TypeName)
    ];

    // Act
    DependencyAnalysis.MethodUse[] violations = [.. DependencyAnalysis
      .MethodUses(application)
      .Where(call => call.OwnerType.StartsWith(contextNamespace + ".", StringComparison.Ordinal))
      .Where(call => repositories.Contains(call.Target) && call.Method is "SaveAsync" or "SaveBatchAsync")
      .Where(call => !call.Target.StartsWith(repositoryNamespace, StringComparison.Ordinal))
    ];

    // Assert
    Assert.Empty(violations);
  }
}