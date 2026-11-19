using System.Collections.Immutable;
using System.Reflection;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.ArchitectureTests.Domain;

public class AggregateOperationInputTests {
  [Trait("ArchitectureRule", "ARCH-DOM-006")]
  [Fact]
  public void NonContainerInputs_AreScalarsOrDomainParameterRecords() {
    // Arrange
    Assembly domainAssembly = typeof(AggregateRoot).Assembly;
    (Type Type, string Path)[] inputs = ReadOperationInputData();
    Type[] scalarTypes = [
      typeof(string),
      typeof(decimal),
      typeof(Guid),
      typeof(Uri),
      typeof(DateTime),
      typeof(DateTimeOffset),
      typeof(DateOnly),
      typeof(TimeOnly),
      typeof(TimeSpan)
    ];

    // Act
    (Type Type, string Path)[] violations = [.. inputs
      .Where(input => !input.Type.IsArray && !input.Type.IsGenericType)
      .Where(input => !input.Type.IsPrimitive && !scalarTypes.Contains(input.Type))
      .Where(input => !(input.Type.Assembly == domainAssembly && input.Type.IsClass &&
        input.Type.GetMethod("<Clone>$") is not null))
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-006")]
  [Fact]
  public void GenericInputs_UseOnlyNullableOrApprovedCollectionShapes() {
    // Arrange
    (Type Type, string Path)[] inputs = ReadOperationInputData();
    Type[] allowedGenericTypes = [
      typeof(Nullable<>),
      typeof(IEnumerable<>),
      typeof(ICollection<>),
      typeof(IReadOnlyCollection<>),
      typeof(IList<>),
      typeof(IReadOnlyList<>),
      typeof(List<>),
      typeof(ImmutableArray<>)
    ];

    // Act
    (Type Type, string Path)[] violations = [.. inputs
      .Where(input => input.Type.IsGenericType)
      .Where(input => !allowedGenericTypes.Contains(input.Type.GetGenericTypeDefinition()))
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-006")]
  [Fact]
  public void ArrayInputs_AreOneDimensional() {
    // Arrange
    (Type Type, string Path)[] inputs = ReadOperationInputData();

    // Act
    (Type Type, string Path)[] violations = [.. inputs
      .Where(input => input.Type.IsArray)
      .Where(input => input.Type.GetArrayRank() != 1)
    ];

    // Assert
    Assert.Empty(violations);
  }

  private static (Type Type, string Path)[] ReadOperationParameters() {
    Assembly domainAssembly = typeof(AggregateRoot).Assembly;
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance |
      BindingFlags.Static | BindingFlags.DeclaredOnly;

    return [.. domainAssembly.GetTypes()
      .Where(type => !type.IsAbstract && typeof(AggregateRoot).IsAssignableFrom(type))
      .SelectMany(type => type.GetMethods(flags))
      .Where(method => !method.IsSpecialName)
      // Restoration is a persistence contract, covered by the lifecycle checks.
      .Where(method => !(method.IsStatic && method.Name == "Rehydrate" && method.ReturnType == method.DeclaringType))
      .SelectMany(method => method.GetParameters().Select(parameter =>
        (parameter.ParameterType, $"{method.DeclaringType?.Name}.{method.Name}({parameter.Name})")))
    ];
  }

  private static (Type Type, string Path)[] ReadOperationInputData() {
    Assembly domainAssembly = typeof(AggregateRoot).Assembly;
    const BindingFlags fieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    var pending = new Queue<(Type Type, string Path)>(ReadOperationParameters());
    var inspected = new HashSet<Type>();
    var inputs = new List<(Type Type, string Path)>();

    while (pending.TryDequeue(out (Type Type, string Path) input)) {
      if (!inspected.Add(input.Type)) {
        continue;
      }
      inputs.Add(input);

      if (input.Type.GetElementType() is Type elementType) {
        pending.Enqueue((elementType, input.Path + "[]"));
      }
      foreach (Type argument in input.Type.GetGenericArguments()) {
        pending.Enqueue((argument, input.Path + "<" + argument.Name + ">"));
      }

      if (input.Type.Assembly != domainAssembly) {
        continue;
      }
      foreach (PropertyInfo property in input.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
        pending.Enqueue((property.PropertyType, $"{input.Path}.{property.Name}"));
      }
      foreach (FieldInfo field in input.Type.GetFields(fieldFlags)) {
        pending.Enqueue((field.FieldType, $"{input.Path}.{field.Name}"));
      }
    }

    return [.. inputs];
  }
}