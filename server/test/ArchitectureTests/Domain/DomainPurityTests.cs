using Metaspesa.ArchitectureTests.Dependencies;
using Metaspesa.Domain.SharedKernel;
using Mono.Cecil;

namespace Metaspesa.ArchitectureTests.Domain;

public class DomainPurityTests {
  [Trait("ArchitectureRule", "ARCH-DOM-007")]
  [Fact]
  public void Domain_DoesNotUseAsynchronousOperations() {
    // Arrange
    using var module = ModuleDefinition.ReadModule(typeof(AggregateRoot).Assembly.Location);
    string[] asynchronousTypes = [
      "System.Collections.Generic.IAsyncEnumerable`1",
      "System.Collections.Generic.IAsyncEnumerator`1",
      "System.IAsyncDisposable",
      "System.Runtime.CompilerServices.AsyncStateMachineAttribute",
      "System.Runtime.CompilerServices.AsyncIteratorStateMachineAttribute"
    ];

    // Act
    string[] references = [.. module.GetTypeReferences()
      .Where(type => type.Namespace == "System.Threading.Tasks"
        || type.Namespace.StartsWith("System.Threading.Tasks.", StringComparison.Ordinal)
        || asynchronousTypes.Contains(type.FullName))
      .Select(type => type.FullName)
    ];

    // Assert
    Assert.Empty(references);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-007")]
  [Fact]
  public void Domain_DoesNotUseInputOrOutputApis() {
    // Arrange
    using var module = ModuleDefinition.ReadModule(typeof(AggregateRoot).Assembly.Location);
    string[] forbiddenNamespaces = ["System.IO", "System.Net"];

    // Act
    string[] references = [.. module.GetTypeReferences()
      .Where(type => type.FullName == "System.Console"
        || forbiddenNamespaces.Any(forbidden => type.Namespace == forbidden
          || type.Namespace.StartsWith(forbidden + ".", StringComparison.Ordinal)))
      .Select(type => type.FullName)
    ];

    // Assert
    Assert.Empty(references);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-007")]
  [Fact]
  public void Domain_DoesNotReadClocks() {
    // Arrange
    DependencyAnalysis.MethodUse[] calls = DependencyAnalysis.MethodUses(typeof(AggregateRoot).Assembly);
    string[] clockTypes = ["System.TimeProvider", "System.Diagnostics.Stopwatch"];
    (string Type, string Member)[] clockMembers = [
      ("System.DateTime", "get_Now"),
      ("System.DateTime", "get_UtcNow"),
      ("System.DateTime", "get_Today"),
      ("System.DateTimeOffset", "get_Now"),
      ("System.DateTimeOffset", "get_UtcNow"),
      ("System.Environment", "get_TickCount"),
      ("System.Environment", "get_TickCount64")
    ];

    // Act
    DependencyAnalysis.MethodUse[] clockReads = [.. calls
      .Where(call => clockTypes.Contains(call.Target)
        || clockMembers.Contains((call.Target, call.Method)))
    ];

    // Assert
    Assert.Empty(clockReads);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-007")]
  [Fact]
  public void Domain_DoesNotUsePersistenceTypesOrAttributes() {
    // Arrange
    using var module = ModuleDefinition.ReadModule(typeof(AggregateRoot).Assembly.Location);
    string[] forbiddenNamespaces = [
      "System.Data",
      "System.ComponentModel.DataAnnotations.Schema",
      "Microsoft.EntityFrameworkCore",
      "Npgsql"
    ];

    // Act
    string[] references = [.. module.GetTypeReferences()
      .Where(type => forbiddenNamespaces.Any(forbidden => type.Namespace == forbidden
        || type.Namespace.StartsWith(forbidden + ".", StringComparison.Ordinal)))
      .Select(type => type.FullName)
    ];

    // Assert
    Assert.Empty(references);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-007")]
  [Fact]
  public void Domain_DoesNotUseTransportTypesOrAttributes() {
    // Arrange
    using var module = ModuleDefinition.ReadModule(typeof(AggregateRoot).Assembly.Location);
    string[] forbiddenNamespaces = ["Microsoft.AspNetCore", "System.Web", "System.ServiceModel", "Grpc"];

    // Act
    string[] references = [.. module.GetTypeReferences()
      .Where(type => forbiddenNamespaces.Any(forbidden => type.Namespace == forbidden
        || type.Namespace.StartsWith(forbidden + ".", StringComparison.Ordinal)))
      .Select(type => type.FullName)
    ];

    // Assert
    Assert.Empty(references);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-007")]
  [Fact]
  public void Domain_DoesNotUseSerializationTypesOrAttributes() {
    // Arrange
    using var module = ModuleDefinition.ReadModule(typeof(AggregateRoot).Assembly.Location);
    string[] forbiddenNamespaces = [
      "System.Text.Json",
      "System.Xml.Serialization",
      "System.Runtime.Serialization",
      "Newtonsoft.Json"
    ];

    // Act
    string[] references = [.. module.GetTypeReferences()
      .Where(type => type.FullName == "System.SerializableAttribute"
        || forbiddenNamespaces.Any(forbidden => type.Namespace == forbidden
          || type.Namespace.StartsWith(forbidden + ".", StringComparison.Ordinal)))
      .Select(type => type.FullName)
    ];

    // Assert
    Assert.Empty(references);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-007")]
  [Fact]
  public void DomainTypes_AreNotMarkedSerializable() {
    // Arrange
    using var module = ModuleDefinition.ReadModule(typeof(AggregateRoot).Assembly.Location);
    // The compiler marks lambda cache classes serializable without a domain annotation.
    TypeDefinition[] domainTypes = [.. module.GetTypes()
      .Where(type => !type.CustomAttributes.Any(attribute =>
        attribute.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute"))
    ];

    // Act
    string[] serializableTypes = [.. domainTypes
      .Where(type => type.IsSerializable)
      .Select(type => type.FullName)
    ];

    // Assert
    Assert.Empty(serializableTypes);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-007")]
  [Fact]
  public void DomainFields_AreNotMarkedNonSerialized() {
    // Arrange
    using var module = ModuleDefinition.ReadModule(typeof(AggregateRoot).Assembly.Location);

    // Act
    string[] nonSerializedFields = [.. module.GetTypes()
      .SelectMany(type => type.Fields)
      .Where(field => field.IsNotSerialized)
      .Select(field => field.FullName)
    ];

    // Assert
    Assert.Empty(nonSerializedFields);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-007")]
  [Fact]
  public void Domain_DoesNotUseInfrastructureFrameworks() {
    // Arrange
    using var module = ModuleDefinition.ReadModule(typeof(AggregateRoot).Assembly.Location);
    string[] forbiddenNamespaces = ["Microsoft.Extensions", "System.Configuration"];

    // Act
    string[] references = [.. module.GetTypeReferences()
      .Where(type => forbiddenNamespaces.Any(forbidden => type.Namespace == forbidden
        || type.Namespace.StartsWith(forbidden + ".", StringComparison.Ordinal)))
      .Select(type => type.FullName)
    ];

    // Assert
    Assert.Empty(references);
  }
}