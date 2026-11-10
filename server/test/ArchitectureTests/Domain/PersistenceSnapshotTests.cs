using System.Collections.Immutable;
using System.Reflection;
using Metaspesa.ArchitectureTests.Dependencies;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.ArchitectureTests.Domain;

public class PersistenceSnapshotTests {
  private sealed class EntityTypes() : TheoryData<Type>(
    typeof(User),
    typeof(Market),
    typeof(Brand),
    typeof(Product),
    typeof(ShoppingList),
    typeof(Purchase),
    typeof(ProductFormat),
    typeof(PriceSnapshot),
    typeof(ShoppingItem),
    typeof(PurchaseItem)
  );

  private sealed class EntityOwnership() : TheoryData<Type, Type>(
    (typeof(User), typeof(User)),
    (typeof(Market), typeof(Market)),
    (typeof(Brand), typeof(Brand)),
    (typeof(Product), typeof(Product)),
    (typeof(ProductFormat), typeof(Product)),
    (typeof(PriceSnapshot), typeof(Product)),
    (typeof(ShoppingList), typeof(ShoppingList)),
    (typeof(ShoppingItem), typeof(ShoppingList)),
    (typeof(Purchase), typeof(Purchase)),
    (typeof(PurchaseItem), typeof(Purchase))
  );

  [Trait("ArchitectureRule", "ARCH-DOM-005")]
  [Theory]
  [ClassData<EntityTypes>]
  public void Entity_ExposesAnImmutablePrimitiveOnlyPersistenceSnapshot(
    Type entityType
  ) {
    // Arrange
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    // Act
    MethodInfo? export = entityType.GetMethod("ToPrimitives", flags, Type.EmptyTypes);
    bool hasValidSnapshot = export is not null &&
      export.ReturnType.FullName == entityType.FullName + "Primitives" &&
      IsImmutablePrimitiveSnapshot(export.ReturnType, []);

    // Assert
    Assert.True(hasValidSnapshot);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-005")]
  [Theory]
  [InlineData(typeof(User), "SaveAsync")]
  [InlineData(typeof(Market), "SaveAsync")]
  [InlineData(typeof(Brand), "SaveAsync")]
  [InlineData(typeof(Product), "SaveBatchAsync")]
  [InlineData(typeof(ShoppingList), "SaveAsync")]
  [InlineData(typeof(Purchase), "SaveAsync")]
  public void AggregateSnapshotExport_IsOnlyCalledByTheOwningRepositorySave(
    Type aggregateType, string saveMethod
  ) {
    // Arrange
    string aggregateName = DependencyAnalysis.TypeName(aggregateType);
    string repositoryName = $"Metaspesa.Database.Repositories.PostgreSql{aggregateType.Name}Repository";
    DependencyAnalysis.MethodUse[] calls = [.. DependencyAnalysis.ProductionAssemblies
      .SelectMany(DependencyAnalysis.MethodUses)
      .Where(call => call.Target == aggregateName && call.Method == "ToPrimitives")
    ];

    // Act
    DependencyAnalysis.MethodUse[] violations = [.. calls
      .Where(call =>
        call.Assembly != "Database" ||
        call.OwnerType != repositoryName ||
        !IsWithinMethod(call, saveMethod))
      ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-005")]
  [Theory]
  [InlineData(typeof(User))]
  [InlineData(typeof(Market))]
  [InlineData(typeof(Brand))]
  [InlineData(typeof(Product))]
  [InlineData(typeof(ShoppingList))]
  [InlineData(typeof(Purchase))]
  public void OwningRepository_UsesTheAggregatePersistenceSnapshot(
    Type aggregateType
  ) {
    // Arrange
    var database = Assembly.Load("Database");
    string aggregateName = DependencyAnalysis.TypeName(aggregateType);
    string repositoryName = $"Metaspesa.Database.Repositories.PostgreSql{aggregateType.Name}Repository";

    // Act
    DependencyAnalysis.MethodUse[] calls = DependencyAnalysis.MethodUses(database);

    // Assert
    Assert.Contains(calls, call => call.OwnerType == repositoryName &&
      call.Target == aggregateName &&
      call.Method == "ToPrimitives");
  }

  [Trait("ArchitectureRule", "ARCH-DOM-005")]
  [Theory]
  [ClassData<EntityOwnership>]
  public void SnapshotMembers_StayInsideTheirEntityAndPersistenceMapping(
    Type entityType, Type aggregateType
  ) {
    // Arrange
    string entityName = DependencyAnalysis.TypeName(entityType);
    string snapshotName = entityName + "Primitives";
    string mapperName = $"Metaspesa.Database.Entities.{entityType.Name}DbEntity";
    string repositoryName = $"Metaspesa.Database.Repositories.PostgreSql{aggregateType.Name}Repository";
    TypeIdentity[] allowedOwners = [
      new("Domain", entityName),
      new("Domain", snapshotName),
      new("Database", mapperName),
      new("Database", repositoryName),
    ];
    Assembly[] assemblies = DependencyAnalysis.ProductionAssemblies;

    // Act
    DependencyAnalysis.MethodUse[] violations = [.. assemblies
      .SelectMany(DependencyAnalysis.MethodUses)
      .Where(call => call.Target == snapshotName)
      .Where(call => !allowedOwners.Contains(TypeIdentity.FromCall(call)))
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-005")]
  [Theory]
  [InlineData(typeof(User))]
  [InlineData(typeof(Market))]
  [InlineData(typeof(Brand))]
  [InlineData(typeof(Product))]
  [InlineData(typeof(ShoppingList))]
  [InlineData(typeof(Purchase))]
  public void RepositoryWrites_ReadSnapshotsInsteadOfTraversingDomainObjects(
    Type aggregateType
  ) {
    // Arrange
    var database = Assembly.Load("Database");
    string repositoryName = $"Metaspesa.Database.Repositories.PostgreSql{aggregateType.Name}Repository";
    string[] writeMethods = ["SaveAsync", "SaveBatchAsync", "InsertProductsAsync", "InsertFormatsAsync", "InsertSnapshotsAsync"];
    string[] snapshotNames = [.. typeof(User).Assembly.GetTypes()
      .Where(type => type.Name.EndsWith("Primitives", StringComparison.Ordinal))
      .Select(DependencyAnalysis.TypeName)];

    // Act
    DependencyAnalysis.MethodUse[] violations = [.. DependencyAnalysis
      .MethodUses(database)
      .Where(call => call.OwnerType == repositoryName &&
        writeMethods.Any(method => IsWithinMethod(call, method)))
      .Where(call =>
        call.Target.StartsWith("Metaspesa.Domain.", StringComparison.Ordinal) &&
        call.Method != "ToPrimitives" &&
        !snapshotNames.Contains(call.Target, StringComparer.Ordinal))
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-005")]
  [Theory]
  [InlineData(typeof(ProductFormat), typeof(Product))]
  [InlineData(typeof(PriceSnapshot), typeof(ProductFormat))]
  [InlineData(typeof(ShoppingItem), typeof(ShoppingList))]
  [InlineData(typeof(PurchaseItem), typeof(Purchase))]
  public void ChildSnapshotExport_IsOnlyCalledByTheEntityOrItsParent(
    Type entityType, Type parentType
  ) {
    // Arrange
    string entityName = DependencyAnalysis.TypeName(entityType);
    string parentName = DependencyAnalysis.TypeName(parentType);
    Assembly[] assemblies = DependencyAnalysis.ProductionAssemblies;

    // Act
    DependencyAnalysis.MethodUse[] violations = [.. assemblies
      .SelectMany(DependencyAnalysis.MethodUses)
      .Where(call => call.Target == entityName &&
        call.Method == "ToPrimitives")
      .Where(call => call.Assembly != "Domain" ||
         call.OwnerType != entityName && call.OwnerType != parentName)
    ];

    // Assert
    Assert.Empty(violations);
  }

  private static bool IsWithinMethod(
    DependencyAnalysis.MethodUse call, string methodName
  ) {
    return call.Caller.Contains("::" + methodName + "(", StringComparison.Ordinal) ||
      call.Caller.Contains("<" + methodName + ">", StringComparison.Ordinal);
  }

  private static bool IsImmutablePrimitiveSnapshot(
    Type type, HashSet<Type> inspected
  ) {
    Type? nullableType = Nullable.GetUnderlyingType(type);
    if (nullableType is not null) {
      return IsImmutablePrimitiveSnapshot(nullableType, inspected);
    }

    if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
      type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateOnly)) {
      return true;
    }

    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>)) {
      return IsImmutablePrimitiveSnapshot(type.GetGenericArguments()[0], inspected);
    }

    if (!type.IsSealed || !type.Name.EndsWith("Primitives", StringComparison.Ordinal) ||
      type.GetMethod("<Clone>$") is null) {
      return false;
    }

    if (!inspected.Add(type)) {
      return true;
    }

    FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);

    return fields.All(field => field.IsInitOnly &&
      IsImmutablePrimitiveSnapshot(field.FieldType, inspected)) &&
      properties.All(property =>
        IsImmutablePrimitiveSnapshot(property.PropertyType, inspected));
  }
}