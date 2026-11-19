using System.Reflection;
using Metaspesa.Application.Identity;
using Metaspesa.ArchitectureTests.Dependencies;
using Mono.Cecil;

namespace Metaspesa.ArchitectureTests.Persistence;

public class PersistenceBoundaryTests {
  [Trait("ArchitectureRule", "ARCH-REP-003")]
  [Fact]
  public void Database_DoesNotReferenceUseCaseHandlers() {
    // Arrange
    using var module = ModuleDefinition.ReadModule(Assembly.Load("Database").Location);
    string[] handlers = [.. typeof(LoginUser).Assembly.GetTypes()
      .Where(type => type.Name == "Handler")
      .Select(DependencyAnalysis.TypeName)
    ];

    // Act
    string[] references = [.. module.GetTypeReferences()
      .Where(type => handlers.Contains(type.FullName))
      .Select(type => type.FullName)
    ];

    // Assert
    Assert.Empty(references);
  }

  [Trait("ArchitectureRule", "ARCH-REP-004")]
  [Fact]
  public void Application_DoesNotReferenceQueryableInterfaces() {
    // Arrange
    using var module = ModuleDefinition.ReadModule(typeof(LoginUser).Assembly.Location);

    // Act
    string[] references = [.. module.GetTypeReferences()
      .Where(type => type.FullName is "System.Linq.IQueryable"
        or "System.Linq.IQueryable`1"
        or "System.Linq.IOrderedQueryable`1")
      .Select(type => type.FullName)
    ];

    // Assert
    Assert.Empty(references);
  }

  [Trait("ArchitectureRule", "ARCH-REP-004")]
  [Fact]
  public void Application_DoesNotReferenceEntityFrameworkTypes() {
    // Arrange
    using var module = ModuleDefinition.ReadModule(typeof(LoginUser).Assembly.Location);

    // Act
    string[] references = [.. module.GetTypeReferences()
      .Where(type => type.Namespace == "Microsoft.EntityFrameworkCore" ||
        type.Namespace.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.Ordinal))
      .Select(type => type.FullName)
    ];

    // Assert
    Assert.Empty(references);
  }
}