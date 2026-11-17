using System.Reflection;
using Metaspesa.Domain.SharedKernel;
using NetArchTest.Rules;
using ArchTestResult = NetArchTest.Rules.TestResult;

namespace Metaspesa.ArchitectureTests.BoundedContexts;

public class SharedKernelTests {
  [Trait("ArchitectureRule", "ARCH-BC-003")]
  [Fact]
  public void SharedKernel_DoesNotContainConcreteAggregateRoots() {
    // Arrange
    Type[] types = typeof(AggregateRoot).Assembly.GetTypes();

    // Act
    Type[] aggregates = [.. types
      .Where(type => type.Namespace?.StartsWith("Metaspesa.Domain.SharedKernel", StringComparison.Ordinal) == true)
      .Where(type => !type.IsAbstract && typeof(AggregateRoot).IsAssignableFrom(type))
    ];

    // Assert
    Assert.Empty(aggregates);
  }

  [Trait("ArchitectureRule", "ARCH-BC-003")]
  [Theory]
  [InlineData("Metaspesa.Domain.Identity")]
  [InlineData("Metaspesa.Domain.Markets")]
  [InlineData("Metaspesa.Domain.Shopping")]
  public void SharedKernel_DoesNotDependOnContextTypes(string contextNamespace) {
    // Arrange
    Assembly domain = typeof(AggregateRoot).Assembly;

    // Act
    ArchTestResult result = Types.InAssembly(domain)
      .That().ResideInNamespaceStartingWith("Metaspesa.Domain.SharedKernel")
      .ShouldNot().HaveDependencyOn(contextNamespace)
      .GetResult();

    // Assert
    Assert.True(result.IsSuccessful);
  }
}