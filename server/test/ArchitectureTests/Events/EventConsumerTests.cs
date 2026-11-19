using System.Reflection;
using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Identity;
using Metaspesa.ArchitectureTests.Dependencies;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.ArchitectureTests.Events;

public class EventConsumerTests {
  [Trait("ArchitectureRule", "ARCH-EVT-004")]
  [Fact]
  public void DomainEventConsumers_LiveInInfrastructure() {
    // Arrange
    Assembly[] assemblies = DependencyAnalysis.ProductionAssemblies;

    // Act
    Type[] violations = [.. assemblies.SelectMany(assembly => assembly.GetTypes())
      .Where(type => !type.IsAbstract && typeof(IDomainEventHandler).IsAssignableFrom(type))
      .Where(type => type.Assembly.GetName().Name != "Infrastructure")
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-EVT-004")]
  [Fact]
  public void EachDomainEventConsumer_HasOneUseCaseInvocationSite() {
    // Arrange
    var infrastructure = Assembly.Load("Infrastructure");
    string[] handlers = [.. typeof(LoginUser).Assembly.GetTypes()
      .Where(type => type.Name == "Handler")
      .Select(DependencyAnalysis.TypeName)
    ];
    Type[] consumers = [.. infrastructure.GetTypes()
      .Where(type => !type.IsAbstract &&
        typeof(IDomainEventHandler).IsAssignableFrom(type))
    ];
    DependencyAnalysis.MethodUse[] calls = DependencyAnalysis.MethodUses(infrastructure);

    // Act
    Type[] violations = [.. consumers.Where(consumer => calls.Count(call =>
      call.OwnerType == DependencyAnalysis.TypeName(consumer) &&
      handlers.Contains(call.Target) &&
      call.Method == "Handle"
    ) != 1)];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-EVT-004")]
  [Fact]
  public void UseCaseInputDtos_DoNotReferenceSourceDomainEvents() {
    // Arrange
    Type[] inputs = [.. typeof(LoginUser).Assembly.GetTypes()
      .Where(type => type.Name.StartsWith("Command", StringComparison.Ordinal) ||
        type.Name == "Query")
    ];
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // Act
    FieldInfo[] violations = [.. inputs.SelectMany(type => type.GetFields(flags))
      .Where(field => ExpandType(field.FieldType).Any(type =>
        typeof(IDomainEvent).IsAssignableFrom(type) ||
        type.Assembly == typeof(IDomainEvent).Assembly &&
        type.Namespace?.EndsWith(".Events", StringComparison.Ordinal) == true))
    ];

    // Assert
    Assert.Empty(violations);
  }

  private static IEnumerable<Type> ExpandType(Type type) {
    yield return type;
    foreach (Type argument in type.GetGenericArguments()) {
      foreach (Type nested in ExpandType(argument)) {
        yield return nested;
      }
    }
  }
}