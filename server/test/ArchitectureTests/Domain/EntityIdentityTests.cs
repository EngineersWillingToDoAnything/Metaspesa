using System.Reflection;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.ArchitectureTests.Domain;

public class EntityIdentityTests {
  [Trait("ArchitectureRule", "ARCH-DOM-009")]
  [Theory]
  [InlineData(typeof(User))]
  [InlineData(typeof(Market))]
  [InlineData(typeof(Brand))]
  [InlineData(typeof(Product))]
  [InlineData(typeof(ProductFormat))]
  [InlineData(typeof(PriceSnapshot))]
  [InlineData(typeof(ShoppingList))]
  [InlineData(typeof(ShoppingItem))]
  [InlineData(typeof(Purchase))]
  [InlineData(typeof(PurchaseItem))]
  public void Entity_ExposesItsTypedIdentity(Type entityType) {
    // Arrange
    string expectedIdentityType = entityType.FullName + "Id";

    // Act
    PropertyInfo? identity = entityType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);

    // Assert
    Assert.Equal(expectedIdentityType, identity?.PropertyType.FullName);
  }

  [Trait("ArchitectureRule", "ARCH-DOM-009")]
  [Theory]
  [InlineData(typeof(User))]
  [InlineData(typeof(Market))]
  [InlineData(typeof(Brand))]
  [InlineData(typeof(Product))]
  [InlineData(typeof(ProductFormat))]
  [InlineData(typeof(PriceSnapshot))]
  [InlineData(typeof(ShoppingList))]
  [InlineData(typeof(ShoppingItem))]
  [InlineData(typeof(Purchase))]
  [InlineData(typeof(PurchaseItem))]
  public void EntityIdentity_HasNoSetter(Type entityType) {
    // Arrange
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

    // Act
    MethodInfo? setter = entityType.GetProperty("Id", flags)?.GetSetMethod(nonPublic: true);

    // Assert
    Assert.Null(setter);
  }
}