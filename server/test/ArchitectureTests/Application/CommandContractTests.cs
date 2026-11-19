using System.Reflection;
using Metaspesa.Application.Identity;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.ArchitectureTests.Application;

public class CommandContractTests {
  [Trait("ArchitectureRule", "ARCH-APP-005")]
  [Fact]
  public void CommandDtoFields_DoNotExposeDomainEntitiesOrValueObjects() {
    // Arrange
    Assembly application = typeof(LoginUser).Assembly;
    Assembly domain = typeof(AggregateRoot).Assembly;
    Type[] commands = [.. application.GetTypes().Where(type =>
      type.Name.StartsWith("Command", StringComparison.Ordinal))
    ];
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // Act
    FieldInfo[] violations = [.. commands
      .SelectMany(type => type.GetFields(flags))
      .Where(field => ExpandType(field.FieldType).Any(type =>
        type.Assembly == domain && type.GetMethod("<Clone>$") is null))
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-APP-005")]
  [Fact]
  public void CommandHandlers_DoNotReturnDomainEntitiesOrValueObjects() {
    // Arrange
    Type[] commandOwners = [.. typeof(LoginUser).Assembly.GetTypes()
      .Where(type => type.GetNestedType("Command") is not null)
    ];
    Assembly domain = typeof(AggregateRoot).Assembly;

    // Act
    MethodInfo[] violations = [.. commandOwners
      .SelectMany(type => type.GetNestedType("Handler")?.GetMethods() ?? [])
      .Where(method => method.Name == "Handle")
      .Where(method => ExpandType(method.ReturnType).Any(type =>
        type.Assembly == domain && type.GetMethod("<Clone>$") is null))
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