using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Metaspesa.ArchitectureTests.Database;

public class MigrationSequenceTests {
  [Trait("ArchitectureRule", "ARCH-DB-003")]
  [Fact]
  public void MigrationIdentifiers_AreGloballyUnique() {
    // Arrange
    var database = Assembly.Load("Database");
    Type[] migrations = [.. database.GetTypes()
      .Where(type => !type.IsAbstract && typeof(Migration).IsAssignableFrom(type))
    ];

    // Act
    string?[] identifiers = [.. migrations
      .Select(type => type.GetCustomAttribute<MigrationAttribute>()?.Id)
    ];

    // Assert
    Assert.Equal(identifiers.Length, identifiers.Distinct(StringComparer.Ordinal).Count());
  }

  [Trait("ArchitectureRule", "ARCH-DB-003")]
  [Fact]
  public void Migrations_DeclareAnIdentifier() {
    // Arrange
    var database = Assembly.Load("Database");
    Type[] migrations = [.. database.GetTypes()
      .Where(type => !type.IsAbstract && typeof(Migration).IsAssignableFrom(type))
    ];

    // Act
    Type[] violations = [.. migrations.Where(type =>
      string.IsNullOrWhiteSpace(type.GetCustomAttribute<MigrationAttribute>()?.Id))
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-DB-003")]
  [Fact]
  public void Migrations_DeclareAContextOwnedByDatabase() {
    // Arrange
    var database = Assembly.Load("Database");
    Type[] migrations = [.. database.GetTypes()
      .Where(type => !type.IsAbstract && typeof(Migration).IsAssignableFrom(type))
    ];

    // Act
    Type[] violations = [.. migrations.Where(type =>
      type.GetCustomAttribute<DbContextAttribute>()?.ContextType.Assembly != database)
    ];

    // Assert
    Assert.Empty(violations);
  }
}