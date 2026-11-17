using System.Reflection;
using Metaspesa.Application.Identity;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.ArchitectureTests.Persistence;

public class RepositoryContractTests {
  [Trait("ArchitectureRule", "ARCH-REP-001")]
  [Theory]
  [InlineData(typeof(User))]
  [InlineData(typeof(Brand))]
  [InlineData(typeof(Market))]
  [InlineData(typeof(Product))]
  [InlineData(typeof(ShoppingList))]
  [InlineData(typeof(Purchase))]
  public void AggregateRoot_HasExactlyOneSpecificWriteRepository(
    Type aggregateType
  ) {
    // Arrange
    Type[] interfaces = [.. typeof(LoginUser).Assembly.GetTypes().Where(type => type.IsInterface)];

    // Act
    Type[] repositories = [.. interfaces.Where(contract => contract.GetMethods()
      .Where(method => method.Name is "SaveAsync" or "SaveBatchAsync")
      .SelectMany(method => method.GetParameters())
      .SelectMany(parameter => ExpandType(parameter.ParameterType))
      .Contains(aggregateType))
    ];

    // Assert
    Assert.Single(repositories);
  }

  [Trait("ArchitectureRule", "ARCH-REP-001")]
  [Fact]
  public void Application_DoesNotExposeGenericRepositoryContracts() {
    // Arrange
    Type[] interfaces = [..
      typeof(LoginUser).Assembly.GetExportedTypes().Where(type => type.IsInterface)
    ];

    // Act
    Type[] genericRepositories = [.. interfaces
      .Where(type => type.IsGenericType)
      .Where(type => type.Name.Split('`')[0].EndsWith("Repository", StringComparison.Ordinal))
    ];

    // Assert
    Assert.Empty(genericRepositories);
  }

  [Trait("ArchitectureRule", "ARCH-REP-002")]
  [Fact]
  public void RepositoryContracts_LiveInApplicationAbstractions() {
    // Arrange
    Type[] repositories = [.. typeof(LoginUser).Assembly.GetTypes()
      .Where(type => type.IsInterface &&
        type.Name.EndsWith("Repository", StringComparison.Ordinal))
    ];

    // Act
    Type[] violations = [.. repositories.Where(type => type.Namespace is null ||
      !type.Namespace.StartsWith("Metaspesa.Application.Abstractions.", StringComparison.Ordinal))
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-REP-002")]
  [Fact]
  public void RepositoryImplementations_LiveInDatabase() {
    // Arrange
    Type[] contracts = [.. typeof(LoginUser).Assembly.GetTypes()
      .Where(type => type.IsInterface && type.Name.EndsWith("Repository", StringComparison.Ordinal))
    ];
    string[] assemblyNames = [
      "Application",
      "Domain",
      "Database",
      "Infrastructure",
      "RestApi",
      "MigrationService",
      "ServiceDefaults"
    ];

    // Act
    Type[] violations = [.. assemblyNames.Select(Assembly.Load)
      .SelectMany(assembly => assembly.GetTypes())
      .Where(type => !type.IsAbstract && type.GetInterfaces().Intersect(contracts).Any())
      .Where(type => type.Assembly.GetName().Name != "Database")
    ];

    // Assert
    Assert.Empty(violations);
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