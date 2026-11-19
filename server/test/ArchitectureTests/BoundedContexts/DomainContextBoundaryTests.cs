using System.Reflection;
using Metaspesa.ArchitectureTests.Dependencies;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.ArchitectureTests.BoundedContexts;

public class DomainContextBoundaryTests {
  private sealed class ContextNamespaces() : TheoryData<string>(
    "Metaspesa.Domain.Identity",
    "Metaspesa.Domain.Markets",
    "Metaspesa.Domain.Shopping"
  );

  [Trait("ArchitectureRule", "ARCH-BC-002")]
  [Theory]
  [ClassData<ContextNamespaces>]
  public void DomainState_DoesNotHoldForeignAggregateRootsOrChildren(
    string contextNamespace
  ) {
    // Arrange
    Assembly domain = typeof(AggregateRoot).Assembly;
    Type[] entities = [
      .. DependencyAnalysis.AggregateTypes.Where(type => !type.IsAbstract),
      typeof(ProductFormat),
      typeof(PriceSnapshot),
      typeof(ShoppingItem),
      typeof(PurchaseItem)
    ];
    Type[] foreignEntities = [..
      entities.Where(type => type.Namespace != contextNamespace)
    ];
    Type[] contextTypes = [.. domain.GetTypes().Where(type =>
      type.Namespace == contextNamespace ||
      type.Namespace?.StartsWith(contextNamespace + ".", StringComparison.Ordinal) == true)
    ];
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    // Act
    FieldInfo[] violations = [.. contextTypes
      .SelectMany(type => type.GetFields(flags))
      .Where(field => ExpandType(field.FieldType).Any(foreignEntities.Contains))
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-BC-002")]
  [Theory]
  [ClassData<ContextNamespaces>]
  public void DomainCode_DoesNotInvokeForeignAggregateRootsOrChildren(
    string contextNamespace
  ) {
    // Arrange
    Assembly domain = typeof(AggregateRoot).Assembly;
    Type[] entities = [
      .. DependencyAnalysis.AggregateTypes.Where(type => !type.IsAbstract),
      typeof(ProductFormat),
      typeof(PriceSnapshot),
      typeof(ShoppingItem),
      typeof(PurchaseItem)
    ];
    string[] foreignEntities = [.. entities
      .Where(type => type.Namespace != contextNamespace)
      .Select(DependencyAnalysis.TypeName)
    ];

    // Act
    DependencyAnalysis.MethodUse[] violations = [.. DependencyAnalysis.MethodUses(domain)
      .Where(call => call.OwnerType.StartsWith(contextNamespace + ".", StringComparison.Ordinal))
      .Where(call => foreignEntities.Contains(call.Target))
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-BC-002")]
  [Theory]
  [ClassData<ContextNamespaces>]
  public void DomainSignatures_DoNotExposeForeignAggregateRootsOrChildren(
    string contextNamespace
  ) {
    // Arrange
    Assembly domain = typeof(AggregateRoot).Assembly;
    Type[] entities = [
      .. DependencyAnalysis.AggregateTypes.Where(type => !type.IsAbstract),
      typeof(ProductFormat), typeof(PriceSnapshot), typeof(ShoppingItem), typeof(PurchaseItem)
    ];
    Type[] foreignEntities = [..
      entities.Where(type => type.Namespace != contextNamespace)
    ];
    Type[] contextTypes = [.. domain.GetTypes()
      .Where(type => type.Namespace == contextNamespace ||
        type.Namespace?.StartsWith(contextNamespace + ".", StringComparison.Ordinal) == true)
    ];
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    MethodBase[] members = [
      .. contextTypes.SelectMany(type => type.GetMethods(flags)),
      .. contextTypes.SelectMany(type => type.GetConstructors(flags))
    ];

    // Act
    MethodBase[] violations = [.. members.Where(member =>
      member.GetParameters()
        .SelectMany(parameter => ExpandType(parameter.ParameterType))
        .Any(foreignEntities.Contains) ||
      member is MethodInfo method && ExpandType(method.ReturnType).Any(foreignEntities.Contains))
    ];

    // Assert
    Assert.Empty(violations);
  }

  private static IEnumerable<Type> ExpandType(Type type) {
    yield return type;
    if (type.HasElementType) {
      foreach (Type element in ExpandType(type.GetElementType()!)) {
        yield return element;
      }
    }
    foreach (Type argument in type.GetGenericArguments()) {
      foreach (Type nested in ExpandType(argument)) {
        yield return nested;
      }
    }
  }
}