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

  private sealed class SnapshotOwners() : TheoryData<Type, Type, string>(
    (typeof(UserPrimitives), typeof(User), "Metaspesa.Database.Repositories.PostgreSqlUserRepository"),
    (typeof(MarketPrimitives), typeof(Market), "Metaspesa.Database.Repositories.PostgreSqlMarketRepository"),
    (typeof(BrandPrimitives), typeof(Brand), "Metaspesa.Database.Repositories.PostgreSqlBrandRepository"),
    (typeof(ProductPrimitives), typeof(Product), "Metaspesa.Database.Repositories.PostgreSqlMarketProductRepository"),
    (typeof(ProductFormatPrimitives), typeof(ProductFormat), "Metaspesa.Database.Repositories.PostgreSqlMarketProductRepository"),
    (typeof(PriceSnapshotPrimitives), typeof(PriceSnapshot), "Metaspesa.Database.Repositories.PostgreSqlMarketProductRepository"),
    (typeof(ShoppingListPrimitives), typeof(ShoppingList), "Metaspesa.Database.Repositories.PostgreSqlShoppingListRepository"),
    (typeof(ShoppingItemPrimitives), typeof(ShoppingItem), "Metaspesa.Database.Repositories.PostgreSqlShoppingListRepository"),
    (typeof(PurchasePrimitives), typeof(Purchase), "Metaspesa.Database.Repositories.PostgreSqlPurchaseRepository"),
    (typeof(PurchaseItemPrimitives), typeof(PurchaseItem), "Metaspesa.Database.Repositories.PostgreSqlPurchaseRepository")
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
      export.ReturnType.Name.EndsWith("Primitives", StringComparison.Ordinal) &&
      IsImmutablePrimitiveSnapshot(export.ReturnType, []);

    // Assert
    Assert.True(hasValidSnapshot);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-005")]
  [Theory]
  [InlineData(typeof(User), "Metaspesa.Database.Repositories.PostgreSqlUserRepository", "SaveAsync")]
  [InlineData(typeof(Market), "Metaspesa.Database.Repositories.PostgreSqlMarketRepository", "SaveAsync")]
  [InlineData(typeof(Brand), "Metaspesa.Database.Repositories.PostgreSqlBrandRepository", "SaveAsync")]
  [InlineData(typeof(Product), "Metaspesa.Database.Repositories.PostgreSqlMarketProductRepository", "SaveBatchAsync")]
  [InlineData(typeof(ShoppingList), "Metaspesa.Database.Repositories.PostgreSqlShoppingListRepository", "SaveAsync")]
  [InlineData(typeof(Purchase), "Metaspesa.Database.Repositories.PostgreSqlPurchaseRepository", "SaveAsync")]
  public void AggregateSnapshotExport_IsOnlyCalledByTheOwningRepositorySave(
    Type aggregateType, string repositoryType, string saveMethod
  ) {
    // Arrange
    string aggregateName = DependencyAnalysis.TypeName(aggregateType);
    DependencyAnalysis.MethodUse[] calls = [.. DependencyAnalysis.ProductionAssemblies
      .SelectMany(DependencyAnalysis.MethodUses)
      .Where(call => call.Target == aggregateName && call.Method == "ToPrimitives")
    ];

    // Act
    DependencyAnalysis.MethodUse[] violations = [.. calls
      .Where(call =>
        call.Assembly != "Database" ||
        call.OwnerType != repositoryType ||
        !IsWithinMethod(call, saveMethod))
      ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-005")]
  [Theory]
  [InlineData(typeof(User), "Metaspesa.Database.Repositories.PostgreSqlUserRepository")]
  [InlineData(typeof(Market), "Metaspesa.Database.Repositories.PostgreSqlMarketRepository")]
  [InlineData(typeof(Brand), "Metaspesa.Database.Repositories.PostgreSqlBrandRepository")]
  [InlineData(typeof(Product), "Metaspesa.Database.Repositories.PostgreSqlMarketProductRepository")]
  [InlineData(typeof(ShoppingList), "Metaspesa.Database.Repositories.PostgreSqlShoppingListRepository")]
  [InlineData(typeof(Purchase), "Metaspesa.Database.Repositories.PostgreSqlPurchaseRepository")]
  public void OwningRepository_UsesTheAggregatePersistenceSnapshot(
    Type aggregateType, string repositoryType
  ) {
    // Arrange
    var database = Assembly.Load("Database");
    string aggregateName = DependencyAnalysis.TypeName(aggregateType);

    // Act
    DependencyAnalysis.MethodUse[] calls = DependencyAnalysis.MethodUses(database);

    // Assert
    Assert.Contains(calls, call => call.OwnerType == repositoryType &&
      call.Target == aggregateName &&
      call.Method == "ToPrimitives");
  }

  [Trait("ArchitectureRule", "ARCH-DOM-005")]
  [Theory]
  [ClassData<SnapshotOwners>]
  public void SnapshotMembers_AreOnlyUsedByTheirOwningEntityAndRepository(
    Type snapshotType, Type entityType, string repositoryType
  ) {
    // Arrange
    string snapshotName = DependencyAnalysis.TypeName(snapshotType);
    string entityName = DependencyAnalysis.TypeName(entityType);
    Assembly[] assemblies = DependencyAnalysis.ProductionAssemblies;

    // Act
    DependencyAnalysis.MethodUse[] violations = [.. assemblies
      .SelectMany(DependencyAnalysis.MethodUses)
      .Where(call => call.Target == snapshotName)
      .Where(call => (
          call.Assembly != "Domain" ||
          call.OwnerType != entityName && call.OwnerType != snapshotName
        ) && (
          call.Assembly != "Database" ||
          call.OwnerType != repositoryType
        ))
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-005")]
  [Theory]
  [InlineData("Metaspesa.Database.Repositories.PostgreSqlUserRepository")]
  [InlineData("Metaspesa.Database.Repositories.PostgreSqlMarketRepository")]
  [InlineData("Metaspesa.Database.Repositories.PostgreSqlBrandRepository")]
  [InlineData("Metaspesa.Database.Repositories.PostgreSqlMarketProductRepository")]
  [InlineData("Metaspesa.Database.Repositories.PostgreSqlShoppingListRepository")]
  [InlineData("Metaspesa.Database.Repositories.PostgreSqlPurchaseRepository")]
  public void RepositoryWrites_ReadSnapshotsInsteadOfTraversingDomainObjects(
    string repositoryType
  ) {
    // Arrange
    var database = Assembly.Load("Database");
    string[] writeMethods = ["SaveAsync", "SaveBatchAsync", "InsertProductsAsync", "InsertFormatsAsync", "InsertSnapshotsAsync"];
    string[] snapshotNames = [.. typeof(User).Assembly.GetTypes()
      .Where(type => type.Name.EndsWith("Primitives", StringComparison.Ordinal))
      .Select(DependencyAnalysis.TypeName)];

    // Act
    DependencyAnalysis.MethodUse[] violations = [.. DependencyAnalysis
      .MethodUses(database)
      .Where(call => call.OwnerType == repositoryType &&
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