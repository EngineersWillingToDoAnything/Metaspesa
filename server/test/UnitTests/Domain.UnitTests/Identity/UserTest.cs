using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Identity.Errors;

namespace Metaspesa.Domain.UnitTests.Identity;

public static class UserTest {
  [Fact]
  public static void Rehydrate_DoesNotRaiseDomainEvents() {
    // Arrange
    var id = new UserId(Guid.Parse("00000000-0000-7000-8000-000000000001"));
    var username = new Username("estela");
    var passwordHash = new PasswordHash("hashed");

    // Act
    var user = User.Rehydrate(id, username, passwordHash, Role.Shopper);

    // Assert
    Assert.Empty(user.DomainEvents);
  }

  [Fact(DisplayName = "Creates shopper user")]
  public static void User_CreatedAsShopper_WhenValuesAreValid() {
    // Act
    var user = User.Create("estela", "hashed");

    // Assert
    Assert.Equal(Role.Shopper, user.Role);
  }

  [Fact(DisplayName = "Creates user with supplied username")]
  public static void User_CreatedWithSuppliedUsername_WhenValuesAreValid() {
    // Act
    var user = User.Create("estela", "hashed");

    // Assert
    Assert.Equal("estela", user.Username.Value);
  }

  [Fact(DisplayName = "Creates user with supplied password hash")]
  public static void User_CreatedWithSuppliedPasswordHash_WhenValuesAreValid() {
    // Act
    var user = User.Create("estela", "hashed");

    // Assert
    Assert.Equal("hashed", user.PasswordHash.Value);
  }

  [Fact(DisplayName = "Creates user with version 7 identifier")]
  public static void User_CreatedWithVersion7Identifier_WhenValuesAreValid() {
    // Act
    var user = User.Create("estela", "hashed");

    // Assert
    Assert.Equal(7, user.Id.Value.Version);
  }

  [Theory(DisplayName = "Rejects invalid username")]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" ")]
  public static void User_ThrowsInvalidUsernameException_WhenUsernameIsInvalid(
    string? username
  ) {
    // Act & Assert
    Assert.Throws<InvalidUsernameException>(() => User.Create(username!, "hashed"));
  }

  [Theory(DisplayName = "Rejects invalid password hash")]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" ")]
  public static void User_ThrowsInvalidPasswordHashException_WhenPasswordHashIsInvalid(
    string? passwordHash
  ) {
    // Act & Assert
    Assert.Throws<InvalidPasswordHashException>(
      () => User.Create("estela", passwordHash!));
  }

  [Fact]
  public static void ToPrimitives_PreservesUserState() {
    // Arrange
    var user = User.Create("shopper", "password_hash");
    var expected = new UserPrimitives(
      user.Id.Value, "shopper", "password_hash", (int)Role.Shopper);

    // Act
    UserPrimitives state = user.ToPrimitives();

    // Assert
    Assert.Equal(expected, state);
  }
}