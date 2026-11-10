using System.Text.Json;

namespace Metaspesa.ArchitectureTests.Dependencies;

public class ProjectDependencyTests {
  [Trait("ArchitectureRule", "ARCH-DEP-001")]
  [Fact]
  public void Domain_HasNoProjectReferences() {
    // Arrange
    using JsonDocument assets = ProjectAssets.Read("Domain");

    // Act
    string[] references = ProjectAssets.ProjectReferences(assets.RootElement);

    // Assert
    Assert.Empty(references);
  }

  [Trait("ArchitectureRule", "ARCH-DEP-001")]
  [Fact]
  public void Application_ReferencesOnlyDomain() {
    // Arrange
    using JsonDocument assets = ProjectAssets.Read("Application");

    // Act
    string[] references = ProjectAssets.ProjectReferences(assets.RootElement);

    // Assert
    Assert.DoesNotContain(references, reference => reference != "Domain");
  }

  [Trait("ArchitectureRule", "ARCH-DEP-001")]
  [Fact]
  public void Database_ReferencesOnlyApplicationAndDomain() {
    // Arrange
    using JsonDocument assets = ProjectAssets.Read("Database");
    string[] allowedProjects = ["Application", "Domain"];

    // Act
    string[] references = ProjectAssets.ProjectReferences(assets.RootElement);

    // Assert
    Assert.DoesNotContain(
      references,
      reference => !allowedProjects.Contains(reference, StringComparer.Ordinal));
  }

  [Trait("ArchitectureRule", "ARCH-DEP-001")]
  [Fact]
  public void Infrastructure_ReferencesOnlyApplicationAndDomain() {
    // Arrange
    using JsonDocument assets = ProjectAssets.Read("Infrastructure");
    string[] allowedProjects = ["Application", "Domain"];

    // Act
    string[] references = ProjectAssets.ProjectReferences(assets.RootElement);

    // Assert
    Assert.DoesNotContain(
      references,
      reference => !allowedProjects.Contains(reference, StringComparer.Ordinal));
  }

  [Trait("ArchitectureRule", "ARCH-DEP-001")]
  [Fact]
  public void RestApi_ReferencesOnlyApprovedCompositionDependencies() {
    // Arrange
    using JsonDocument assets = ProjectAssets.Read("RestApi");
    string[] allowedProjects = [
      "Application", "Domain", "Database", "Infrastructure", "ServiceDefaults"
    ];

    // Act
    string[] references = ProjectAssets.ProjectReferences(assets.RootElement);

    // Assert
    Assert.DoesNotContain(
      references,
       reference => !allowedProjects.Contains(reference, StringComparer.Ordinal));
  }

  [Trait("ArchitectureRule", "ARCH-DEP-004")]
  [Fact]
  public void MigrationService_ReferencesOnlyDatabaseAndServiceDefaults() {
    // Arrange
    using JsonDocument assets = ProjectAssets.Read("MigrationService");
    string[] allowedProjects = ["Database", "ServiceDefaults"];

    // Act
    string[] references = ProjectAssets.ProjectReferences(assets.RootElement);

    // Assert
    Assert.DoesNotContain(
      references,
      reference => !allowedProjects.Contains(reference, StringComparer.Ordinal));
  }

  [Trait("ArchitectureRule", "ARCH-DEP-001")]
  [Fact]
  public void ServiceDefaults_HasNoProjectReferences() {
    // Arrange
    using JsonDocument assets = ProjectAssets.Read("ServiceDefaults");

    // Act
    string[] references = ProjectAssets.ProjectReferences(assets.RootElement);

    // Assert
    Assert.Empty(references);
  }
}