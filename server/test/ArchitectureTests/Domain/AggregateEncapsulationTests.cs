using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using Metaspesa.ArchitectureTests.Dependencies;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.ArchitectureTests.Domain;

public class AggregateEncapsulationTests {
  [Trait("ArchitectureRule", "ARCH-DOM-002")]
  [Fact]
  public void DomainState_IsNotExposedThroughPublicInstanceFields() {
    // Arrange
    Assembly domainAssembly = typeof(ShoppingList).Assembly;
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    // Act
    FieldInfo[] fields = [.. domainAssembly.GetTypes()
      .Where(type => !type.IsEnum)
      .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
      .SelectMany(type => type.GetFields(flags))
    ];

    // Assert
    Assert.Empty(fields);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-002")]
  [Fact]
  public void AggregateState_CannotBeChangedThroughPublicPropertySetters() {
    // Arrange
    Type[] aggregates = DependencyAnalysis.AggregateTypes;
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

    // Act
    PropertyInfo[] properties = [.. aggregates
      .SelectMany(type => type.GetProperties(flags))
      .Where(property => property.SetMethod?.IsPublic == true)
    ];

    // Assert
    Assert.Empty(properties);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-002")]
  [Theory]
  [InlineData(typeof(ShoppingItem))]
  [InlineData(typeof(PurchaseItem))]
  [InlineData(typeof(ProductFormat))]
  [InlineData(typeof(PriceSnapshot))]
  public void ChildEntityState_CannotBeChangedThroughPublicPropertySetters(
    Type entityType
  ) {
    // Arrange
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

    // Act
    PropertyInfo[] properties = [.. entityType
      .GetProperties(flags)
      .Where(property => property.SetMethod?.IsPublic == true)
    ];

    // Assert
    Assert.Empty(properties);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-002")]
  [Fact]
  public void PublicInstanceMembers_DoNotReturnChildEntities() {
    // Arrange
    Type[] children = [
      typeof(ShoppingItem),
      typeof(PurchaseItem),
      typeof(ProductFormat),
      typeof(PriceSnapshot)
    ];
    Type[] entities = [
      .. DependencyAnalysis.AggregateTypes.Where(type => !type.IsAbstract),
      .. children
    ];
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    // Act
    MethodInfo[] violations = [.. entities
      .SelectMany(type => type.GetMethods(flags))
      .Where(method => ContainsChildEntity(method.ReturnType, children))
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-002")]
  [Fact]
  public void PublicStateCollections_AreImmutable() {
    // Arrange
    Type[] children = [
      typeof(ShoppingItem),
      typeof(PurchaseItem),
      typeof(ProductFormat),
      typeof(PriceSnapshot)
    ];
    Type[] entities = [
      .. DependencyAnalysis.AggregateTypes.Where(type => !type.IsAbstract),
      .. children
    ];
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    // Act
    MethodInfo[] violations = [.. entities
      .SelectMany(type => type.GetMethods(flags))
      .Where(method => IsCollection(method.ReturnType))
      .Where(method => !method.ReturnType.IsGenericType ||
        method.ReturnType.GetGenericTypeDefinition() != typeof(ImmutableArray<>))
    ];

    // Assert
    Assert.Empty(violations);
  }

  private static bool ContainsChildEntity(Type type, Type[] children) {
    if (children.Contains(type)) {
      return true;
    }

    if (type.HasElementType) {
      return ContainsChildEntity(type.GetElementType()!, children);
    }

    return type.GetGenericArguments().Any(argument => ContainsChildEntity(argument, children));
  }

  private static bool IsCollection(Type type) {
    if (type == typeof(string)) {
      return false;
    }

    return type == typeof(System.Collections.IEnumerable) ||
      type.GetInterfaces().Append(type).Any(candidate => candidate.IsGenericType &&
        candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
  }
}