using System.Reflection;
using Metaspesa.Application.Identity;
using Metaspesa.Domain.Identity;
using NetArchTest.Rules;
using ArchTestResult = NetArchTest.Rules.TestResult;

namespace Metaspesa.ArchitectureTests.Dependencies;

public class LayerDependencyTests {
  [Trait("ArchitectureRule", "ARCH-DEP-001")]
  [Theory(DisplayName = "Domain has no dependency on outer server layers")]
  [InlineData("Metaspesa.Application")]
  [InlineData("Metaspesa.Database")]
  [InlineData("Metaspesa.Infrastructure")]
  [InlineData("Metaspesa.RestApi")]
  [InlineData("Metaspesa.MigrationService")]
  public void Domain_DoesNotDependOnOuterLayer(string forbiddenNamespace) {
    // Arrange
    Assembly assembly = typeof(User).Assembly;

    // Act
    ArchTestResult result = FindDependencies(assembly, forbiddenNamespace);

    // Assert
    Assert.True(result.IsSuccessful);
  }

  [Trait("ArchitectureRule", "ARCH-DEP-001")]
  [Theory(DisplayName = "Application has no dependency on implementation layers")]
  [InlineData("Metaspesa.Database")]
  [InlineData("Metaspesa.Infrastructure")]
  [InlineData("Metaspesa.RestApi")]
  [InlineData("Metaspesa.MigrationService")]
  public void Application_DoesNotDependOnImplementationLayer(
    string forbiddenNamespace
  ) {
    // Arrange
    Assembly assembly = typeof(LoginUser).Assembly;

    // Act
    ArchTestResult result = FindDependencies(assembly, forbiddenNamespace);

    // Assert
    Assert.True(result.IsSuccessful);
  }

  [Trait("ArchitectureRule", "ARCH-DEP-001")]
  [Theory(DisplayName = "View layers have no dependency on other views")]
  [InlineData("MigrationService", "Metaspesa.RestApi")]
  [InlineData("RestApi", "Metaspesa.MigrationService")]
  public void ViewLayer_DoesNotDependOnOtherView(
    string assemblyName,
    string forbiddenNamespace
  ) {
    // Arrange
    var assembly = Assembly.Load(assemblyName);

    // Act
    ArchTestResult result = FindDependencies(assembly, forbiddenNamespace);

    // Assert
    Assert.True(result.IsSuccessful);
  }

  private static ArchTestResult FindDependencies(
    Assembly assembly, string forbiddenNamespace
  ) {
    return Types.InAssembly(assembly)
      .ShouldNot()
      .HaveDependencyOn(forbiddenNamespace)
      .GetResult();
  }
}