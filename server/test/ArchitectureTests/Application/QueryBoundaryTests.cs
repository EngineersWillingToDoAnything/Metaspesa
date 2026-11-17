using System.Reflection;
using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Identity;
using Metaspesa.ArchitectureTests.Dependencies;

namespace Metaspesa.ArchitectureTests.Application;

public class QueryBoundaryTests {
  [Trait("ArchitectureRule", "ARCH-APP-004")]
  [Fact]
  public void Queries_DoNotCallRepositoryWrites() {
    // Arrange
    Assembly assembly = typeof(LoginUser).Assembly;
    string[] queryOwners = [.. assembly.GetTypes()
      .Where(type => type.GetNestedType("Query") is not null)
      .Select(DependencyAnalysis.TypeName)
    ];
    string[] writeMethods = ["SaveAsync", "SaveBatchAsync", "DeleteAsync"];

    // Act
    DependencyAnalysis.MethodUse[] writes = [.. DependencyAnalysis
      .MethodUses(assembly)
      .Where(call => queryOwners.Contains(call.OwnerType))
      .Where(call => call.Target.StartsWith("Metaspesa.Application.Abstractions.", StringComparison.Ordinal))
      .Where(call => writeMethods.Contains(call.Method))
    ];

    // Assert
    Assert.Empty(writes);
  }

  [Trait("ArchitectureRule", "ARCH-APP-004")]
  [Fact]
  public void Queries_DoNotPublishDomainEvents() {
    // Arrange
    Assembly assembly = typeof(LoginUser).Assembly;
    string publisher = DependencyAnalysis.TypeName(typeof(IDomainEventPublisher));
    string[] queryOwners = [.. assembly.GetTypes()
      .Where(type => type.GetNestedType("Query") is not null)
      .Select(DependencyAnalysis.TypeName)
    ];

    // Act
    DependencyAnalysis.MethodUse[] publications = [.. DependencyAnalysis
      .MethodUses(assembly)
      .Where(call => queryOwners.Contains(call.OwnerType))
      .Where(call => call.Target == publisher && call.Method == "PublishAsync")
    ];

    // Assert
    Assert.Empty(publications);
  }
}