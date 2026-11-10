using System.Text.Json;

namespace Metaspesa.ArchitectureTests.Dependencies;

public class PackageDependencyTests {
  private const string SonarAnalyzerNuget = "SonarAnalyzer.CSharp";

  [Trait("ArchitectureRule", "ARCH-DEP-002")]
  [Fact]
  public void Domain_ReferencesOnlyTheSonarBuildTool() {
    // Arrange
    using JsonDocument assets = ProjectAssets.Read("Domain");

    // Act
    string[] packages = ProjectAssets.DirectPackages(assets.RootElement);

    // Assert
    Assert.DoesNotContain(packages, package => package != SonarAnalyzerNuget);
  }

  [Trait("ArchitectureRule", "ARCH-DEP-003")]
  [Fact]
  public void Application_ReferencesOnlyApprovedAbstractionsAndTheSonarBuildTool() {
    // Arrange
    using JsonDocument assets = ProjectAssets.Read("Application");
    string[] approvedPackages = [
      SonarAnalyzerNuget,
      "Microsoft.Extensions.DependencyInjection.Abstractions",
      "Microsoft.Extensions.Logging.Abstractions",
    ];

    // Act
    string[] packages = ProjectAssets.DirectPackages(assets.RootElement);

    // Assert
    Assert.DoesNotContain(
      packages,
      package => !approvedPackages.Contains(package, StringComparer.Ordinal));
  }

  [Trait("ArchitectureRule", "ARCH-DEP-002")]
  [Fact]
  public void Domain_HasNoExternalRuntimeOrCompilePackages() {
    // Arrange
    using JsonDocument assets = ProjectAssets.Read("Domain");

    // Act
    string[] packages = ProjectAssets.RuntimePackages(assets.RootElement);

    // Assert
    Assert.Empty(packages);
  }

  [Trait("ArchitectureRule", "ARCH-DEP-003")]
  [Fact]
  public void Application_RuntimeAndCompilePackagesAreApprovedAbstractions() {
    // Arrange
    using JsonDocument assets = ProjectAssets.Read("Application");
    string[] approvedPackages = [
      "Microsoft.Extensions.DependencyInjection.Abstractions",
    ];

    // Act
    string[] packages = ProjectAssets.RuntimePackages(assets.RootElement);

    // Assert
    Assert.DoesNotContain(
      packages,
      package => !approvedPackages.Contains(package, StringComparer.Ordinal));
  }

  [Trait("ArchitectureRule", "ARCH-DEP-002")]
  [Theory]
  [InlineData("Domain")]
  [InlineData("Application")]
  [InlineData("Database")]
  [InlineData("Infrastructure")]
  [InlineData("RestApi")]
  [InlineData("MigrationService")]
  [InlineData("ServiceDefaults")]
  public void ProductionProject_InheritsTheSonarAnalyzer(string project) {
    // Arrange
    using JsonDocument assets = ProjectAssets.Read(project);

    // Act
    string[] packages = ProjectAssets.DirectPackages(assets.RootElement);

    // Assert
    Assert.Contains(SonarAnalyzerNuget, packages);
  }

  [Trait("ArchitectureRule", "ARCH-DEP-002")]
  [Fact]
  public void Domain_UsesOnlyTheDotNetCoreFramework() {
    // Arrange
    using JsonDocument assets = ProjectAssets.Read("Domain");

    // Act
    string[] frameworks = FrameworkReferences(assets.RootElement);

    // Assert
    Assert.DoesNotContain(
      frameworks,
      framework => framework != "Microsoft.NETCore.App");
  }

  [Trait("ArchitectureRule", "ARCH-DEP-003")]
  [Fact]
  public void Application_UsesOnlyTheDotNetCoreFramework() {
    // Arrange
    using JsonDocument assets = ProjectAssets.Read("Application");

    // Act
    string[] frameworks = FrameworkReferences(assets.RootElement);

    // Assert
    Assert.DoesNotContain(
      frameworks,
      framework => framework != "Microsoft.NETCore.App");
  }

  private static string[] FrameworkReferences(JsonElement assets) {
    return [.. assets.GetProperty("project")
      .GetProperty("frameworks")
      .EnumerateObject()
      .SelectMany(framework => framework.Value.GetProperty("frameworkReferences").EnumerateObject())
      .Select(framework => framework.Name)];
  }
}