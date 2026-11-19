using System.Reflection;
using Metaspesa.ArchitectureTests.Dependencies;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Shopping;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Metaspesa.ArchitectureTests.Domain;

public class AggregateLifecycleTests {
  [Trait("ArchitectureRule", "ARCH-DOM-004")]
  [Fact]
  public void Rehydration_DoesNotCallValueObjectConstructorsThatValidate() {
    // Arrange
    using var module = ModuleDefinition.ReadModule(typeof(User).Assembly.Location);
    string[] validatingTypes = [.. module.GetTypes()
      .Where(type => type.IsValueType)
      .Where(type => type.Methods.Any(method => method.IsConstructor && method.HasBody &&
        method.Body.Instructions.Any(instruction => instruction.OpCode == OpCodes.Throw)))
      .Select(type => type.FullName)
    ];

    // Act
    DependencyAnalysis.MethodUse[] violations = [.. DependencyAnalysis.MethodUses(typeof(User).Assembly)
      .Where(call => call.Caller.Contains("::Rehydrate(", StringComparison.Ordinal))
      .Where(call => call.Method == ".ctor" && validatingTypes.Contains(call.Target))
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-004")]
  [Fact]
  public void Rehydration_DoesNotCallValueObjectCreationFactories() {
    // Arrange
    string[] valueObjects = [.. typeof(User).Assembly.GetTypes()
      .Where(type => type.IsValueType && !type.IsEnum)
      .Select(DependencyAnalysis.TypeName)
    ];

    // Act
    DependencyAnalysis.MethodUse[] violations = [.. DependencyAnalysis.MethodUses(typeof(User).Assembly)
      .Where(call => call.Caller.Contains("::Rehydrate(", StringComparison.Ordinal))
      .Where(call => call.Method == "Create" && valueObjects.Contains(call.Target))
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-004")]
  [Fact]
  public void ValueObjects_DoNotDeclareRehydrationFactories() {
    // Arrange
    Type[] valueObjects = [.. typeof(User).Assembly.GetTypes()
      .Where(type => type.IsValueType && !type.IsEnum)
    ];
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

    // Act
    MethodInfo[] violations = [.. valueObjects.SelectMany(type => type.GetMethods(flags))
      .Where(method => method.Name == "Rehydrate")
    ];

    // Assert
    Assert.Empty(violations);
  }

  private sealed class AggregateTypes() : TheoryData<Type>(
    typeof(User),
    typeof(Market),
    typeof(Brand),
    typeof(Product),
    typeof(ShoppingList),
    typeof(Purchase)
  );

  [Trait("ArchitectureRule", "ARCH-DOM-004")]
  [Theory]
  [ClassData<AggregateTypes>]
  public void AggregateRoot_ExposesAnExplicitCreationFactory(Type aggregateType) {
    // Arrange
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

    // Act
    MethodInfo[] factories = aggregateType.GetMethods(flags);

    // Assert
    Assert.Contains(
      factories,
      method => method.Name == "Create" && method.ReturnType == aggregateType);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-004")]
  [Theory]
  [ClassData<AggregateTypes>]
  public void AggregateRoot_ExposesAnExplicitRehydrationFactory(Type aggregateType) {
    // Arrange
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

    // Act
    MethodInfo[] factories = aggregateType.GetMethods(flags);

    // Assert
    Assert.Contains(factories,
      method => method.Name == "Rehydrate" &&
      method.ReturnType == aggregateType);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-004")]
  [Theory]
  [ClassData<AggregateTypes>]
  public void AggregateRoot_CannotBeConstructedThroughAPublicConstructor(
    Type aggregateType
  ) {
    // Arrange
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

    // Act
    ConstructorInfo[] constructors = aggregateType.GetConstructors(flags);

    // Assert
    Assert.Empty(constructors);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-004")]
  [Fact]
  public void AggregateRehydration_IsOnlyCalledByDatabaseProductionCode() {
    // Arrange
    Assembly[] assemblies = DependencyAnalysis.ProductionAssemblies;
    string[] aggregateNames = [..
      DependencyAnalysis.AggregateTypes.Select(DependencyAnalysis.TypeName)
    ];

    // Act
    DependencyAnalysis.MethodUse[] violations = [.. assemblies
      .Where(assembly => assembly.GetName().Name != "Database")
      .SelectMany(DependencyAnalysis.MethodUses)
      .Where(call =>
        call.Method == "Rehydrate" &&
        aggregateNames.Contains(call.Target, StringComparer.Ordinal))
    ];

    // Assert
    Assert.Empty(violations);
  }
}