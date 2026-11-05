using System.Reflection;
using Metaspesa.ArchitectureTests.Dependencies;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.ArchitectureTests.Domain;

public class AggregateLifecycleTests {
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