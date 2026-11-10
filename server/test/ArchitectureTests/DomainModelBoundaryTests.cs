using System.Reflection;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.ArchitectureTests;

public class DomainModelBoundaryTests {
  [Fact(DisplayName = "Shopping references no Markets aggregates")]
  public void Shopping_DoesNotReferenceMarketsAggregates() {
    // Arrange
    var forbiddenTypes = new HashSet<Type> {
      typeof(Market),
      typeof(Product),
      typeof(ProductFormat),
      typeof(PriceSnapshot),
    };

    // Act
    Type[] violations = typeof(ShoppingList).Assembly.GetTypes()
      .Where(type => type.Namespace?.StartsWith(
        "Metaspesa.Domain.Shopping",
        StringComparison.Ordinal) == true)
      .Where(type => ReferencedTypes(type).Any(forbiddenTypes.Contains))
      .ToArray();

    // Assert
    Assert.Empty(violations);
  }

  [Fact(DisplayName = "Product format snapshot contains only pending price observations as a collection")]
  public void ProductFormatSnapshot_ContainsOnlyPendingSnapshotCollection() {
    // Arrange
    Type productFormatType = typeof(ProductFormatPrimitives);
    string[] expectedCollections = [nameof(ProductFormatPrimitives.PendingSnapshots)];

    // Act
    string[] snapshotCollections = [.. productFormatType.GetProperties()
      .Where(property => typeof(IEnumerable<PriceSnapshotPrimitives>).IsAssignableFrom(property.PropertyType))
      .Select(property => property.Name)];

    // Assert
    Assert.Equal(expectedCollections, snapshotCollections);
  }

  [Fact(DisplayName = "Price snapshot has no ProductFormat back-reference")]
  public void PriceSnapshot_DoesNotReferenceProductFormatId() {
    // Arrange
    Type snapshotType = typeof(PriceSnapshot);

    // Act
    Type[] referencedTypes = [.. ReferencedTypes(snapshotType)];

    // Assert
    Assert.DoesNotContain(typeof(ProductFormatId), referencedTypes);
  }

  [Fact(DisplayName = "Product format snapshot contains its latest price observation")]
  public void ProductFormatSnapshot_ContainsLatestSnapshot() {
    // Arrange
    Type productFormatType = typeof(ProductFormatPrimitives);

    // Act
    Type? snapshotType = productFormatType.GetProperty(nameof(ProductFormatPrimitives.LatestSnapshot))?.PropertyType;

    // Assert
    Assert.Equal(typeof(PriceSnapshotPrimitives), snapshotType);
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