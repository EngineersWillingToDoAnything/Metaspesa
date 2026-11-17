using Metaspesa.Application.Identity;
using Metaspesa.ArchitectureTests.Dependencies;
using Mono.Cecil;

namespace Metaspesa.ArchitectureTests.Application;

public class ApplicationEnvironmentTests {
  [Trait("ArchitectureRule", "ARCH-APP-007")]
  [Fact]
  public void Application_DoesNotDependOnConfigurationProviders() {
    // Arrange
    using var module = ModuleDefinition.ReadModule(typeof(LoginUser).Assembly.Location);
    string[] forbiddenNamespaces = [
      "Microsoft.Extensions.Configuration", "System.Configuration"
    ];

    // Act
    string[] references = [.. module.GetTypeReferences()
      .Where(type => forbiddenNamespaces.Any(forbidden => type.Namespace == forbidden ||
        type.Namespace.StartsWith(forbidden + ".", StringComparison.Ordinal)))
      .Select(type => type.FullName)
    ];

    // Assert
    Assert.Empty(references);
  }

  [Trait("ArchitectureRule", "ARCH-APP-007")]
  [Fact]
  public void Application_DoesNotUseFileSystemApis() {
    // Arrange
    using var module = ModuleDefinition.ReadModule(typeof(LoginUser).Assembly.Location);

    // Act
    string[] references = [.. module.GetTypeReferences()
      .Where(type => type.Namespace == "System.IO" ||
        type.Namespace.StartsWith("System.IO.", StringComparison.Ordinal))
      .Select(type => type.FullName)
    ];

    // Assert
    Assert.Empty(references);
  }

  [Trait("ArchitectureRule", "ARCH-APP-007")]
  [Fact]
  public void Application_DoesNotReadEnvironmentVariables() {
    // Arrange
    DependencyAnalysis.MethodUse[] calls = DependencyAnalysis.MethodUses(typeof(LoginUser).Assembly);

    // Act
    DependencyAnalysis.MethodUse[] reads = [.. calls
      .Where(call => call.Target == "System.Environment")
      .Where(call => call.Method is "GetEnvironmentVariable"
        or "GetEnvironmentVariables"
        or "ExpandEnvironmentVariables")
    ];

    // Assert
    Assert.Empty(reads);
  }

  [Trait("ArchitectureRule", "ARCH-APP-007")]
  [Fact]
  public void Application_DoesNotReadAmbientClocks() {
    // Arrange
    DependencyAnalysis.MethodUse[] calls = DependencyAnalysis.MethodUses(typeof(LoginUser).Assembly);
    string[] clockTypes = ["System.Diagnostics.Stopwatch"];
    (string Type, string Member)[] clockMembers = [
      ("System.DateTime", "get_Now"),
      ("System.DateTime", "get_UtcNow"),
      ("System.DateTime", "get_Today"),
      ("System.DateTimeOffset", "get_Now"),
      ("System.DateTimeOffset", "get_UtcNow"),
      ("System.Environment", "get_TickCount"),
      ("System.Environment", "get_TickCount64"),
      ("System.TimeProvider", "get_System")
    ];

    // Act
    DependencyAnalysis.MethodUse[] reads = [.. calls
      .Where(call => clockTypes.Contains(call.Target) ||
        clockMembers.Contains((call.Target, call.Method)))
    ];

    // Assert
    Assert.Empty(reads);
  }
}