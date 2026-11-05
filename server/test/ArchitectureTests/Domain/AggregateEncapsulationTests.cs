using System.Reflection;
using System.Runtime.CompilerServices;
using Metaspesa.ArchitectureTests.Dependencies;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.ArchitectureTests.Domain;

public class AggregateEncapsulationTests {
  [Trait("ArchitectureRule", "ARCH-DOM-002")]
  [Fact]
  public void DomainState_IsNotExposedThroughPublicInstanceFields() {
    // Arrange
    Assembly domainAssembly = typeof(ShoppingList).Assembly;
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    // Act
    FieldInfo[] fields = [.. domainAssembly.GetTypes()
      .Where(type => !type.IsEnum)
      .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
      .SelectMany(type => type.GetFields(flags))
    ];

    // Assert
    Assert.Empty(fields);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-002")]
  [Fact]
  public void AggregateState_CannotBeChangedThroughPublicPropertySetters() {
    // Arrange
    Type[] aggregates = DependencyAnalysis.AggregateTypes;
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

    // Act
    PropertyInfo[] properties = [.. aggregates
      .SelectMany(type => type.GetProperties(flags))
      .Where(property => property.SetMethod?.IsPublic == true)
    ];

    // Assert
    Assert.Empty(properties);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-002")]
  [Theory]
  [InlineData(typeof(ShoppingItem))]
  [InlineData(typeof(PurchaseItem))]
  [InlineData(typeof(ProductFormat))]
  [InlineData(typeof(PriceSnapshot))]
  public void ChildEntityState_CannotBeChangedThroughPublicPropertySetters(
    Type entityType
  ) {
    // Arrange
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

    // Act
    PropertyInfo[] properties = [.. entityType
      .GetProperties(flags)
      .Where(property => property.SetMethod?.IsPublic == true)
    ];

    // Assert
    Assert.Empty(properties);
  }
}