using System.Reflection;

namespace Metaspesa.ArchitectureTests.Dependencies;

public class ServiceLocationTests {
  private static readonly TypeIdentity[] AllowedServiceLocationBoundaries = [
    new("RestApi", "Program"),
    new("MigrationService", "Program"),
    new("Application", "Metaspesa.Application.ApplicationDependencyInjection"),
    new("Database", "Metaspesa.Database.DatabaseDependencyInjection"),
    new("Infrastructure", "Metaspesa.Infrastructure.InfrastructureDependencyInjection"),
    new("RestApi", "Metaspesa.RestApi.ServiceCollectionExtensions"),
    new("RestApi", "Metaspesa.RestApi.OpenApiExporter"),
    new("ServiceDefaults", "Metaspesa.ServiceDefaults.ServiceDefaultExtensions"),
  ];

  [Trait("ArchitectureRule", "ARCH-DEP-006")]
  [Fact]
  public void ContainerCalls_OnlyOccurAtApprovedBoundariesOrInScopeCreatingWorkers() {
    // Arrange
    Assembly[] assemblies = DependencyAnalysis.ProductionAssemblies;
    Type[] types = [.. assemblies.SelectMany(assembly => assembly.GetTypes())];
    var analysis = new ServiceLocationAnalysis(types);

    // Act
    DependencyAnalysis.MethodUse[] violations = analysis.FindUnauthorizedContainerCalls(
      AllowedServiceLocationBoundaries);

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-DEP-006")]
  [Fact]
  public void ContainerInjection_OnlyOccursAtApprovedBoundariesOrInScopeCreatingWorkers() {
    // Arrange
    Assembly[] assemblies = DependencyAnalysis.ProductionAssemblies;
    Type[] types = [.. assemblies.SelectMany(assembly => assembly.GetTypes())];
    var analysis = new ServiceLocationAnalysis(types);

    // Act
    Type[] violations = analysis
      .FindUnauthorizedContainerInjection(AllowedServiceLocationBoundaries);

    // Assert
    Assert.Empty(violations);
  }
}