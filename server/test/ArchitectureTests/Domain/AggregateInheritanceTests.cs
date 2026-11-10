using Metaspesa.ArchitectureTests.Dependencies;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.ArchitectureTests.Domain;

public class AggregateInheritanceTests {
  private sealed class AggregateTypes() : TheoryData<Type>(
    typeof(User),
    typeof(Market),
    typeof(Brand),
    typeof(Product),
    typeof(ShoppingList),
    typeof(Purchase)
  );

  [Trait("ArchitectureRule", "ARCH-DOM-001")]
  [Theory]
  [ClassData<AggregateTypes>]
  public void AggregateRoot_InheritsTheSharedBase(Type aggregateType) {
    // Arrange
    Type baseType = typeof(AggregateRoot);

    // Act
    bool inheritsBase = baseType.IsAssignableFrom(aggregateType);

    // Assert
    Assert.True(inheritsBase);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-001")]
  [Fact]
  public void AggregatesDiscoveredFromWriteRepositories_InheritTheSharedBase() {
    // Arrange
    Type[] aggregates = DependencyAnalysis.AggregateTypes;
    Type baseType = typeof(AggregateRoot);

    // Act
    Type[] violations = [..
      aggregates.Where(type => !baseType.IsAssignableFrom(type))
    ];

    // Assert
    Assert.Empty(violations);
  }
}