using Metaspesa.Domain.SharedKernel.Errors;

namespace Metaspesa.ArchitectureTests.Domain;

public class DomainFailureTests {
  [Trait("ArchitectureRule", "ARCH-DOM-008")]
  [Fact]
  public void DomainExceptions_DeriveFromTheDomainExceptionBase() {
    // Arrange
    Type[] domainTypes = typeof(DomainException).Assembly.GetTypes();

    // Act
    Type[] violations = [.. domainTypes
      .Where(type => !type.IsAbstract && typeof(Exception).IsAssignableFrom(type))
      .Where(type => !typeof(DomainException).IsAssignableFrom(type))
    ];

    // Assert
    Assert.Empty(violations);
  }
}