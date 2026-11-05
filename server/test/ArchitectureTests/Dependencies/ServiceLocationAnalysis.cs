using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace Metaspesa.ArchitectureTests.Dependencies;

internal sealed class ServiceLocationAnalysis {
  private readonly Type[] _types;
  private readonly DependencyAnalysis.MethodUse[] _calls;

  internal ServiceLocationAnalysis(Type[] types) {
    _types = types;
    TypeIdentity[] owners = [.. types.Select(type => TypeIdentity.FromType(DependencyAnalysis.Owner(type))).Distinct()];
    Assembly[] assemblies = [.. types.Select(type => type.Assembly).Distinct()];
    _calls = [.. assemblies.SelectMany(DependencyAnalysis.MethodUses)
      .Where(call => owners.Contains(TypeIdentity.FromCall(call)))];
  }

  private Type[] FindScopeCreatingWorkers() {
    var workers = new List<Type>();

    foreach (Type type in _types) {
      if (!typeof(IHostedService).IsAssignableFrom(type)) {
        continue;
      }

      var workerIdentity = TypeIdentity.FromType(type);
      bool createsScope = _calls.Any(call =>
        TypeIdentity.FromCall(call) == workerIdentity && IsScopeCreation(call));

      if (createsScope) {
        workers.Add(type);
      }
    }

    return [.. workers];
  }

  internal DependencyAnalysis.MethodUse[] FindUnauthorizedContainerCalls(
    TypeIdentity[] allowedBoundaries
  ) {
    HashSet<TypeIdentity> allowedOwners = GetAllowedOwners(allowedBoundaries);
    var violations = new List<DependencyAnalysis.MethodUse>();

    foreach (DependencyAnalysis.MethodUse call in _calls) {
      if (IsContainerOperation(call) && !allowedOwners.Contains(TypeIdentity.FromCall(call))) {
        violations.Add(call);
      }
    }

    return [.. violations];
  }

  internal Type[] FindUnauthorizedContainerInjection(
    TypeIdentity[] allowedBoundaries
  ) {
    HashSet<TypeIdentity> allowedOwners = GetAllowedOwners(allowedBoundaries);
    var violations = new List<Type>();

    foreach (Type type in _types) {
      Type owner = DependencyAnalysis.Owner(type);
      if (allowedOwners.Contains(TypeIdentity.FromType(owner))) {
        continue;
      }

      ConstructorInfo[] constructors = type.GetConstructors(
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      bool receivesContainer = constructors.SelectMany(constructor => constructor.GetParameters())
        .Any(parameter => IsContainerAbstraction(parameter.ParameterType));

      if (receivesContainer) {
        violations.Add(type);
      }
    }

    return [.. violations];
  }

  private HashSet<TypeIdentity> GetAllowedOwners(TypeIdentity[] allowedBoundaries) {
    var allowedOwners = new HashSet<TypeIdentity>(allowedBoundaries);
    foreach (Type worker in FindScopeCreatingWorkers()) {
      allowedOwners.Add(TypeIdentity.FromType(worker));
    }

    return allowedOwners;
  }

  private static bool IsScopeCreation(DependencyAnalysis.MethodUse call) {
    bool isScopeApi = call.Target is
      "Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions" or
      "Microsoft.Extensions.DependencyInjection.IServiceScopeFactory";

    return isScopeApi && call.Method is "CreateScope" or "CreateAsyncScope";
  }

  private static bool IsContainerAbstraction(Type type) {
    return type.FullName is
      "System.IServiceProvider" or
      "Microsoft.Extensions.DependencyInjection.IServiceScopeFactory" or
      "Microsoft.Extensions.DependencyInjection.IKeyedServiceProvider";
  }

  private static bool IsContainerOperation(DependencyAnalysis.MethodUse call) {
    bool isContainerApi = call.Target == "System.IServiceProvider" ||
      call.Target.StartsWith("Microsoft.Extensions.DependencyInjection.", StringComparison.Ordinal);

    return isContainerApi && call.Method is
      "GetService" or
      "GetRequiredService" or
      "GetServices" or
      "GetKeyedService" or
      "GetRequiredKeyedService" or
      "GetKeyedServices" or
      "CreateScope" or
      "CreateAsyncScope" or
      "BuildServiceProvider" or
      "GetServiceOrCreateInstance";
  }
}

internal sealed record TypeIdentity(string AssemblyName, string TypeName) {
  internal static TypeIdentity FromType(Type type) {
    return new(type.Assembly.GetName().Name!, DependencyAnalysis.TypeName(type));
  }

  internal static TypeIdentity FromCall(DependencyAnalysis.MethodUse call) {
    return new(call.Assembly, call.OwnerType);
  }
}