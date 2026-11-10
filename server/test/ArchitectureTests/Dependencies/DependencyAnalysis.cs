using System.Reflection;
using Metaspesa.Application.Identity;
using Metaspesa.Domain.SharedKernel;
using Mono.Cecil;

namespace Metaspesa.ArchitectureTests.Dependencies;

internal static class DependencyAnalysis {
  internal static Assembly[] ProductionAssemblies => [..
    ProjectAssets.Projects.Select(Assembly.Load)
  ];

  internal static Type[] AggregateTypes => [..
    typeof(LoginUser).Assembly.GetTypes()
    .Where(type => type.IsInterface && type.Name.EndsWith("Repository", StringComparison.Ordinal))
    .SelectMany(type => type.GetMethods())
    .Where(method => method.Name is "SaveAsync" or "SaveBatchAsync")
    .SelectMany(method => method.GetParameters()).SelectMany(parameter => Expand(parameter.ParameterType))
    .Where(type => type.IsClass && type.Assembly == typeof(AggregateRoot).Assembly)
    .Concat(typeof(AggregateRoot).Assembly.GetTypes()
      .Where(type => !type.IsAbstract && typeof(AggregateRoot).IsAssignableFrom(type)))
    .Append(typeof(AggregateRoot))
    .Distinct()
  ];

  private static IEnumerable<Type> Expand(Type type) {
    yield return type;
    if (type.HasElementType) {
      foreach (Type element in Expand(type.GetElementType()!)) {
        yield return element;
      }
    }
    foreach (Type argument in type.GetGenericArguments()) {
      foreach (Type nested in Expand(argument)) {
        yield return nested;
      }
    }
  }

  // Includes async state machines, lambdas and delegate method references.
  internal static MethodUse[] MethodUses(Assembly assembly) {
    using var module = ModuleDefinition.ReadModule(assembly.Location);
    return [.. AllTypes(module.Types)
      .SelectMany(type => type.Methods)
      .Where(method => method.HasBody)
      .SelectMany(method => method.Body.Instructions
        .Select(instruction => instruction.Operand)
        .OfType<MethodReference>()
        .Select(target => new MethodUse(
          assembly.GetName().Name!,
          TopLevel(method.DeclaringType).FullName,
          method.FullName,
          target.DeclaringType.GetElementType().FullName,
          target.Name)))
    ];
  }

  internal static string TypeName(Type type) => type.FullName!.Replace('+', '/');

  internal static Type Owner(Type type) =>
    type.DeclaringType is null ? type : Owner(type.DeclaringType);

  private static TypeDefinition TopLevel(TypeDefinition type) =>
    type.DeclaringType is null ? type : TopLevel(type.DeclaringType);

  private static IEnumerable<TypeDefinition> AllTypes(
    IEnumerable<TypeDefinition> types
  ) {
    foreach (TypeDefinition type in types) {
      yield return type;
      foreach (TypeDefinition nested in AllTypes(type.NestedTypes)) {
        yield return nested;
      }
    }
  }

  internal sealed record MethodUse(
    string Assembly, string OwnerType, string Caller, string Target, string Method);
}