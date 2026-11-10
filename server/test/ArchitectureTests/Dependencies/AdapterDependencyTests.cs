using System.Reflection;
using Metaspesa.Domain.Markets;

namespace Metaspesa.ArchitectureTests.Dependencies;

public class AdapterDependencyTests {
  [Trait("ArchitectureRule", "ARCH-DEP-005")]
  [Fact]
  public void RestApi_DoesNotCallAggregateConstructorsOrOperations() {
    // Arrange
    var assembly = Assembly.Load("RestApi");
    Type[] aggregates = DependencyAnalysis.AggregateTypes;
    string[] names = [.. aggregates.Select(DependencyAnalysis.TypeName)];

    // Act
    DependencyAnalysis.MethodUse[] violations = [.. DependencyAnalysis
      .MethodUses(assembly)
      .Where(use => names.Contains(use.Target, StringComparer.Ordinal))
    ];

    // Assert
    Assert.Empty(violations);
  }
}