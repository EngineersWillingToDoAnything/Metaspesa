using System.Reflection;
using Metaspesa.ArchitectureTests.Dependencies;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.ArchitectureTests.Domain;

public class ChildEntityBoundaryTests {
  private sealed class DomainEntities() : TheoryData<Type>(
    typeof(ShoppingItem),
    typeof(PurchaseItem),
    typeof(ProductFormat),
    typeof(PriceSnapshot)
  );

  [Trait("ArchitectureRule", "ARCH-DOM-003")]
  [Theory]
  [ClassData<DomainEntities>]
  public void ChildEntity_HasNoPublicConstructor(Type entityType) {
    // Arrange
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

    // Act
    ConstructorInfo[] constructors = entityType.GetConstructors(flags);

    // Assert
    Assert.Empty(constructors);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-003")]
  [Theory]
  [ClassData<DomainEntities>]
  public void ChildEntity_CreationFactoryIsNotPublic(Type entityType) {
    // Arrange
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

    // Act
    MethodInfo[] creationFactories = [..
      entityType.GetMethods(flags).Where(method => method.Name == "Create")
    ];

    // Assert
    Assert.Empty(creationFactories);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-003")]
  [Theory]
  [InlineData(typeof(ShoppingItem), "Update")]
  [InlineData(typeof(ShoppingItem), "Delete")]
  [InlineData(typeof(ProductFormat), "RecordSnapshot")]
  public void ChildEntity_MutationOperationIsNotPublic(
    Type entityType, string operation
  ) {
    // Arrange
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    // Act
    MethodInfo[] publicOperations = [..
      entityType.GetMethods(flags).Where(method => method.Name == operation)
    ];

    // Assert
    Assert.Empty(publicOperations);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-003")]
  [Fact]
  public void ShoppingItemCreationAndMutation_StayInsideShoppingList() {
    // Arrange
    Type entityType = typeof(ShoppingItem);
    Type[] allowedCallers = [typeof(ShoppingItem), typeof(ShoppingList)];

    // Act
    DependencyAnalysis.MethodUse[] violations = FindCallsOutsideAggregate(
      entityType, allowedCallers);

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-003")]
  [Fact]
  public void PurchaseItemCreation_StaysInsidePurchase() {
    // Arrange
    Type entityType = typeof(PurchaseItem);
    Type[] allowedCallers = [typeof(PurchaseItem), typeof(Purchase)];

    // Act
    DependencyAnalysis.MethodUse[] violations = FindCallsOutsideAggregate(
      entityType, allowedCallers);

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-003")]
  [Fact]
  public void ProductFormatCreationAndMutation_StayInsideProduct() {
    // Arrange
    Type entityType = typeof(ProductFormat);
    Type[] allowedCallers = [typeof(ProductFormat), typeof(Product)];

    // Act
    DependencyAnalysis.MethodUse[] violations = FindCallsOutsideAggregate(
      entityType, allowedCallers);

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-003")]
  [Fact]
  public void PriceSnapshotCreation_StaysInsideProduct() {
    // Arrange
    Type entityType = typeof(PriceSnapshot);
    Type[] allowedCallers = [
      typeof(PriceSnapshot), typeof(ProductFormat), typeof(Product)
    ];

    // Act
    DependencyAnalysis.MethodUse[] violations = FindCallsOutsideAggregate(
      entityType, allowedCallers);

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-003")]
  [Fact]
  public void ChildEntityRehydration_IsOnlyCalledByDatabaseProductionCode() {
    // Arrange
    Type[] entities = [
      typeof(ShoppingItem),
      typeof(PurchaseItem),
      typeof(ProductFormat),
      typeof(PriceSnapshot),
    ];
    string[] entityNames = [.. entities.Select(DependencyAnalysis.TypeName)];
    Assembly[] assemblies = DependencyAnalysis.ProductionAssemblies;

    // Act
    DependencyAnalysis.MethodUse[] violations = [.. assemblies
      .Where(assembly => assembly.GetName().Name != "Database")
      .SelectMany(DependencyAnalysis.MethodUses)
      .Where(call => call.Method == "Rehydrate" &&
        entityNames.Contains(call.Target, StringComparer.Ordinal))
    ];

    // Assert
    Assert.Empty(violations);
  }

  private static DependencyAnalysis.MethodUse[] FindCallsOutsideAggregate(
    Type entityType, Type[] allowedCallers
  ) {
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
      BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    string[] restrictedMethods = [.. entityType
      .GetMethods(flags)
      .Where(method => !method.IsSpecialName && method.Name != "Rehydrate")
      .Select(method => method.Name)
      .Append(".ctor")
    ];
    string entityName = DependencyAnalysis.TypeName(entityType);
    string[] callerNames = [.. allowedCallers.Select(DependencyAnalysis.TypeName)];
    string domainAssembly = entityType.Assembly.GetName().Name!;

    return [.. DependencyAnalysis.ProductionAssemblies
      .SelectMany(DependencyAnalysis.MethodUses)
      .Where(call =>
        call.Target == entityName &&
        restrictedMethods.Contains(call.Method, StringComparer.Ordinal))
      .Where(call =>
        call.Assembly != domainAssembly ||
        !callerNames.Contains(call.OwnerType, StringComparer.Ordinal))
    ];
  }
}