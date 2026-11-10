using System.Reflection;
using Metaspesa.Domain.Identity;

namespace Metaspesa.ArchitectureTests.RestApi;

public class ResponseModelBoundaryTests {
  [Trait("ArchitectureRule", "ARCH-API-001")]
  [Theory(DisplayName = "View layers expose no Domain response models")]
  [InlineData("RestApi", "Metaspesa.RestApi")]
  [InlineData("MigrationService", "Metaspesa.MigrationService")]
  public void ViewLayer_DoesNotExposeDomainResponseModels(
    string assemblyName,
    string viewNamespace
  ) {
    // Arrange
    var viewAssembly = Assembly.Load(assemblyName);

    // Act
    Type[] domainResponseTypes = [.. viewAssembly
      .GetTypes()
      .Where(type => type.Namespace is not null &&
        (type.Namespace == viewNamespace ||
          type.Namespace.StartsWith(viewNamespace + ".", StringComparison.Ordinal)))
      .SelectMany(type => type.GetMethods(
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly))
      .SelectMany(method => ExpandType(method.ReturnType))
      .Where(type => type.Assembly == typeof(User).Assembly)
      .Distinct()
    ];

    // Assert
    Assert.Empty(domainResponseTypes);
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