using System.Reflection;
using Metaspesa.Application.Identity;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.ArchitectureTests.RestApi;

public class TransportDtoTests {
  [Trait("ArchitectureRule", "ARCH-API-001")]
  [Fact]
  public void HttpDtoData_DoesNotExposeApplicationTypes() {
    // Arrange
    var restApi = Assembly.Load("RestApi");
    Assembly application = typeof(LoginUser).Assembly;
    Type[] dtos = [.. restApi.GetTypes().Where(type =>
      type.Name.EndsWith("Request", StringComparison.Ordinal) ||
      type.Name.EndsWith("Response", StringComparison.Ordinal))
    ];
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // Act
    Type[] violations = [.. dtos.SelectMany(type =>
      type.GetFields(flags).Select(field => field.FieldType)
        .Concat(type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(property => property.PropertyType)))
      .SelectMany(ExpandType)
      .Where(type => type.Assembly == application)
      .Distinct()
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-API-001")]
  [Fact]
  public void HttpDtoData_DoesNotExposeDomainTypes() {
    // Arrange
    var restApi = Assembly.Load("RestApi");
    Assembly domain = typeof(AggregateRoot).Assembly;
    Type[] dtos = [.. restApi.GetTypes().Where(type =>
      type.Name.EndsWith("Request", StringComparison.Ordinal) ||
      type.Name.EndsWith("Response", StringComparison.Ordinal))
    ];
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // Act
    Type[] violations = [.. dtos.SelectMany(type =>
      type.GetFields(flags).Select(field => field.FieldType)
        .Concat(type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(property => property.PropertyType)))
      .SelectMany(ExpandType)
      .Where(type => type.Assembly == domain)
      .Distinct()
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