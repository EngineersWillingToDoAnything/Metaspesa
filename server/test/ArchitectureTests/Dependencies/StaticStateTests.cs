using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Metaspesa.ArchitectureTests.Dependencies;

public class StaticStateTests {
  [Trait("ArchitectureRule", "ARCH-DEP-007")]
  [Theory]
  [InlineData("Domain")]
  [InlineData("Application")]
  public void InnerLayer_HasNoMutableHandwrittenStaticFields(
    string assemblyName
  ) {
    // Arrange
    var assembly = Assembly.Load(assemblyName);

    // Act
    string[] violations = [.. assembly.GetTypes()
      .Where(type => !IsGenerated(type))
      .SelectMany(type => type.GetFields(BindingFlags.Static | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
      .Where(field => !field.IsLiteral &&
        (!field.IsInitOnly || !IsImmutable(field.FieldType, [])))
      .Select(field => field.DeclaringType!.FullName + "." + field.Name)
    ];

    // Assert
    Assert.Empty(violations);
  }

  private static bool IsImmutable(Type type, HashSet<Type> visited) {
    bool isPrimitive = type.IsPrimitive ||
      type.IsEnum ||
      type == typeof(string) ||
      type == typeof(decimal) ||
      type == typeof(Guid) ||
      type == typeof(DateTime) ||
      type == typeof(DateOnly) ||
      type == typeof(DateTimeOffset) ||
      type == typeof(TimeSpan);

    if (isPrimitive) { return true; }

    bool isPointerType = type.IsArray ||
      type.IsInterface ||
      type.IsAbstract ||
      type == typeof(object) ||
      !type.IsValueType && !type.IsSealed ||
      typeof(Delegate).IsAssignableFrom(type);

    if (isPointerType) { return false; }

    if (!visited.Add(type)) { return true; }

    bool isImmutableArray = type.IsGenericType &&
      type.GetGenericTypeDefinition() == typeof(ImmutableArray<>);
    if (isImmutableArray) {
      return IsImmutable(type.GetGenericArguments()[0], visited);
    }
    return InstanceFields(type).All(field =>
      field.IsInitOnly &&
      IsImmutable(field.FieldType, visited));
  }

  private static IEnumerable<FieldInfo> InstanceFields(Type type) =>
    type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
      .Concat(type.BaseType is null ? [] : InstanceFields(type.BaseType));

  private static bool IsGenerated(Type type) => type.IsDefined(typeof(CompilerGeneratedAttribute)) ||
    type.IsDefined(typeof(GeneratedCodeAttribute)) || type.DeclaringType is not null && IsGenerated(type.DeclaringType);

}