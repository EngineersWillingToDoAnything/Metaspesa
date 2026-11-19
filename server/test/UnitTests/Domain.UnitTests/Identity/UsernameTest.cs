using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Identity.Errors;

namespace Metaspesa.Domain.UnitTests.Identity;

public static class UsernameTest {
  [Theory]
  [InlineData("")]
  [InlineData("legacy user")]
  [InlineData(" legacy_user ")]
  public static void Constructor_PreservesStoredTextWithoutValidationOrTrimming(
    string storedName
  ) {
    // Act
    var username = new Username(storedName);

    // Assert
    Assert.Equal(storedName, username.Value);
  }

  [Theory(DisplayName = "Creates username from valid value")]
  [InlineData("estela")]
  [InlineData("ESTELA")]
  [InlineData("123456")]
  [InlineData("_")]
  [InlineData("Estela_123")]
  public static void Username_Created_WhenValueContainsOnlyAllowedCharacters(
    string value
  ) {
    // Act
    var username = Username.Create(value);

    // Assert
    Assert.Equal(value, username.Value);
  }

  [Fact(DisplayName = "Trims surrounding whitespace from username")]
  public static void Username_Trimmed_WhenValueHasSurroundingWhitespace() {
    // Act
    var username = Username.Create("  Estela_123  ");

    // Assert
    Assert.Equal("Estela_123", username.Value);
  }

  [Theory(DisplayName = "Throws specific exception when username is invalid")]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" ")] // empty
  [InlineData("user name")] // has space
  [InlineData("user-name")] // has hyphen
  [InlineData("user.name")] // has dot
  [InlineData("user@name")] // has @
  [InlineData("user!")] // has exclamation mark
  [InlineData("estelá")] // has accent
  public static void Username_ThrowsInvalidUsernameException_WhenValueIsInvalid(
    string? value
  ) {
    // Act & Assert
    Assert.Throws<InvalidUsernameException>(() => Username.Create(value!));
  }

  [Fact(DisplayName = "Two usernames with the same value are equal")]
  public static void Username_Equal_WhenSameValue() {
    // Act & Assert
    Assert.Equal(Username.Create("estela"), Username.Create("estela"));
  }
}