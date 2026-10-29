using System.Reflection;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.ArchitectureTests;

public class DomainModelBoundaryTests {
  [Fact(DisplayName = "Shopping references no Markets aggregates")]
  public void Shopping_DoesNotReferenceMarketsAggregates() {
    var forbiddenTypes = new HashSet<Type> {
      typeof(Market),
      typeof(Product),
      typeof(ProductFormat),
      typeof(PriceSnapshot),
    };

    Type[] violations = typeof(ShoppingList).Assembly.GetTypes()
      .Where(type => type.Namespace?.StartsWith(
        "Metaspesa.Domain.Shopping",
        StringComparison.Ordinal) == true)
      .Where(type => ReferencedTypes(type).Any(forbiddenTypes.Contains))
      .ToArray();

    Assert.Empty(violations);
  }

  [Fact(DisplayName = "Product exposes only pending snapshots as a collection")]
  public void Product_ExposesOnlyPendingSnapshotCollection() {
    IEnumerable<string> snapshotCollections = typeof(Product).GetProperties()
      .Where(property => typeof(IEnumerable<PriceSnapshot>).IsAssignableFrom(property.PropertyType))
      .Select(property => property.Name);

    Assert.Equal([nameof(Product.PendingSnapshots)], snapshotCollections);
  }

  [Fact(DisplayName = "Product format holds latest snapshot without history")]
  public void ProductFormat_HoldsLatestSnapshotWithoutHistory() {
    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    Assert.DoesNotContain(typeof(ProductFormat).GetFields(flags),
      field => typeof(IEnumerable<PriceSnapshot>).IsAssignableFrom(field.FieldType));
  }

  [Fact(DisplayName = "Product format exposes its latest snapshot")]
  public void ProductFormat_ExposesLatestSnapshot() {
    Assert.Equal(typeof(PriceSnapshot),
      typeof(ProductFormat).GetProperty(nameof(ProductFormat.LatestSnapshot))!.PropertyType);
  }

  [Fact(DisplayName = "Product formats cannot be constructed or mutated publicly")]
  public void ProductFormat_HasNoPublicMutationPath() {
    IEnumerable<string> mutationPaths = typeof(ProductFormat).GetConstructors()
      .Select(constructor => constructor.Name)
      .Concat(typeof(ProductFormat).GetProperties()
        .Where(property => property.SetMethod?.IsPublic == true)
        .Select(property => property.Name));

    Assert.Empty(mutationPaths);
  }

  private static IEnumerable<Type> ReferencedTypes(Type type) {
    const BindingFlags flags =
      BindingFlags.Instance |
      BindingFlags.Static |
      BindingFlags.Public |
      BindingFlags.NonPublic |
      BindingFlags.DeclaredOnly;

    IEnumerable<Type> directTypes = type.GetFields(flags)
      .Select(field => field.FieldType)
      .Concat(type.GetProperties(flags).Select(property => property.PropertyType))
      .Concat(type.GetConstructors(flags)
        .SelectMany(constructor => constructor.GetParameters())
        .Select(parameter => parameter.ParameterType))
      .Concat(type.GetMethods(flags).Select(method => method.ReturnType))
      .Concat(type.GetMethods(flags)
        .SelectMany(method => method.GetParameters())
        .Select(parameter => parameter.ParameterType));

    return directTypes.SelectMany(ExpandType).Distinct();
  }

  private static IEnumerable<Type> ExpandType(Type type) {
    Type normalizedType = type.HasElementType
      ? type.GetElementType()!
      : type;
    yield return normalizedType;

    if (!normalizedType.IsGenericType) {
      yield break;
    }

    foreach (Type argument in normalizedType.GetGenericArguments()) {
      foreach (Type nestedType in ExpandType(argument)) {
        yield return nestedType;
      }
    }
  }
}