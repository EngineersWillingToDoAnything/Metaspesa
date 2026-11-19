using Metaspesa.Database.Entities;
using Metaspesa.Database.Repositories;
using Metaspesa.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Metaspesa.Database.IntegrationTests.Users;

public static class PostgreSqlUserRepositoryTests {
  private static readonly UserRoleDbEntity TestRole = new() {
    Name = "TestRole",
    Description = "Role for testing",
  };

  [Collection("Database")]
  public class CheckUsernameExistsAsync : IAsyncLifetime {
    private readonly MainContext _context;
    private readonly PostgreSqlUserRepository _repository;

    public CheckUsernameExistsAsync(DatabaseFixture fixture) {
      _context = fixture.CreateContext();
      _repository = new PostgreSqlUserRepository(
        _context);
    }

    public async ValueTask InitializeAsync() {
      await _context.Users.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
      await _context.UserRoles.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() {
      await _context.DisposeAsync();
      GC.SuppressFinalize(this);
    }

    [Fact(
      DisplayName = "Returns false when username does not exist")]
    public async Task CheckUsernameExistsAsync_ReturnsFalse_WhenUsernameDoesNotExist() {
      // Act
      bool result = await _repository.CheckUsernameExistsAsync(
        "nonexistent", TestContext.Current.CancellationToken);

      // Assert
      Assert.False(result);
    }

    [Fact(
      DisplayName = "Returns true when username exists")]
    public async Task CheckUsernameExistsAsync_ReturnsTrue_WhenUsernameExists() {
      // Arrange
      var uid = Guid.CreateVersion7();
      _context.Users.Add(new UserDbEntity {
        Uid = uid, Username = "estela", EncryptedPassword = "x", Role = TestRole
      });
      await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

      // Act
      bool result = await _repository.CheckUsernameExistsAsync(
        "estela", TestContext.Current.CancellationToken);

      // Assert
      Assert.True(result);
    }

    [Fact(
      DisplayName = "Returns true case-insensitively")]
    public async Task CheckUsernameExistsAsync_ReturnsTrue_CaseInsensitively() {
      // Arrange
      var uid = Guid.CreateVersion7();
      _context.Users.Add(new UserDbEntity {
        Uid = uid, Username = "Alice", EncryptedPassword = "x", Role = TestRole
      });
      await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

      // Act
      bool result = await _repository.CheckUsernameExistsAsync(
        "ALICE", TestContext.Current.CancellationToken);

      // Assert
      Assert.True(result);
    }

    [Fact(
      DisplayName = "Returns true after trimming")]
    public async Task CheckUsernameExistsAsync_ReturnsTrue_AfterTrimming() {
      // Arrange
      var uid = Guid.CreateVersion7();
      _context.Users.Add(new UserDbEntity {
        Uid = uid, Username = "Pedro", EncryptedPassword = "x", Role = TestRole
      });
      await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

      // Act
      bool result = await _repository.CheckUsernameExistsAsync(
        "   Pedro   ", TestContext.Current.CancellationToken);

      // Assert
      Assert.True(result);
    }
  }

  [Collection("Database")]
  public class SaveAsync : IAsyncLifetime {
    private readonly MainContext _context;
    private readonly PostgreSqlUserRepository _repository;

    public SaveAsync(DatabaseFixture fixture) {
      _context = fixture.CreateContext();
      _repository = new PostgreSqlUserRepository(
        _context);
    }

    public async ValueTask InitializeAsync() {
      await _context.Users.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
      await _context.UserRoles.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() {
      await _context.DisposeAsync();
      GC.SuppressFinalize(this);
    }

    [Fact(
      DisplayName = "Persists user so username can be found afterwards")]
    public async Task SaveAsync_PersistsUser_SoUsernameCanBeFound() {
      // Arrange
      _context.UserRoles.Add(new UserRoleDbEntity {
        Id = UserRoleIds.Shopper,
        Name = Role.Shopper.ToString(),
        Description = "Shopper role for testing",
      });
      await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
      User user = CreateUser("bob", "hashed_password");

      // Act
      await _repository.SaveAsync(user, TestContext.Current.CancellationToken);

      // Assert
      bool exists = await _repository.CheckUsernameExistsAsync(
        "bob", TestContext.Current.CancellationToken);
      Assert.True(exists);
    }

    [Fact(
      DisplayName = "Persists encrypted password correctly")]
    public async Task SaveAsync_PersistsEncryptedPassword_Correctly() {
      // Arrange
      _context.UserRoles.Add(new UserRoleDbEntity {
        Id = UserRoleIds.Shopper,
        Name = Role.Shopper.ToString(),
        Description = "Shopper role for testing",
      });
      await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

      const string HashedPassword = "hashed_password_value";
      User user = CreateUser("carol", HashedPassword);

      // Act
      await _repository.SaveAsync(user, TestContext.Current.CancellationToken);

      // Assert
      User? retrieved = await _repository.GetUserByUsernameAsync(
        Username.Create("carol"), TestContext.Current.CancellationToken);
      Assert.Equal(HashedPassword, retrieved!.PasswordHash.Value);
    }

    [Fact(
      DisplayName = "Persists Shopper role correctly")]
    public async Task SaveAsync_PersistsShopperRole_Correctly() {
      // Arrange
      _context.UserRoles.Add(new UserRoleDbEntity {
        Id = UserRoleIds.Shopper,
        Name = Role.Shopper.ToString(),
        Description = "Shopper role for testing",
      });
      await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

      User user = CreateUser("dave", "hashed_password");

      // Act
      await _repository.SaveAsync(user, TestContext.Current.CancellationToken);

      // Assert
      User? retrieved = await _repository.GetUserByUsernameAsync(
        Username.Create("dave"), TestContext.Current.CancellationToken);
      Assert.Equal(Role.Shopper, retrieved!.Role);
    }
  }

  [Collection("Database")]
  public class GetUserByUsernameAsync : IAsyncLifetime {
    private readonly MainContext _context;
    private readonly PostgreSqlUserRepository _repository;

    public GetUserByUsernameAsync(DatabaseFixture fixture) {
      _context = fixture.CreateContext();
      _repository = new PostgreSqlUserRepository(
        _context);
    }

    public async ValueTask InitializeAsync() {
      await _context.Users.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
      await _context.UserRoles.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() {
      await _context.DisposeAsync();
      GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetUserByUsernameAsync_RestoresStoredHashWithoutCreationValidation() {
      // Arrange
      var role = new UserRoleDbEntity {
        Id = UserRoleIds.Shopper,
        Name = nameof(Role.Shopper),
        Description = "Shopper role for testing"
      };
      var persisted = new UserDbEntity {
        Uid = Guid.CreateVersion7(),
        Username = "legacy_user",
        EncryptedPassword = "",
        RoleId = role.Id,
        Role = role
      };
      _context.Users.Add(persisted);
      await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
      _context.ChangeTracker.Clear();

      // Act
      User? restored = await _repository.GetUserByUsernameAsync(
        Username.Create(persisted.Username), TestContext.Current.CancellationToken);

      // Assert
      Assert.Equal(persisted.EncryptedPassword, restored?.PasswordHash.Value);
    }

    [Fact(
      DisplayName = "Returns null when user does not exist")]
    public async Task GetUserByUsernameAsync_ReturnsNull_WhenUserDoesNotExist() {
      // Act
      User? result = await _repository.GetUserByUsernameAsync(
        Username.Create("nobody"), TestContext.Current.CancellationToken);

      // Assert
      Assert.Null(result);
    }

    [Fact(
      DisplayName = "Returns user when username matches")]
    public async Task GetUserByUsernameAsync_ReturnsUser_WhenUsernameMatches() {
      // Arrange
      _context.UserRoles.Add(new UserRoleDbEntity {
        Id = UserRoleIds.Shopper,
        Name = Role.Shopper.ToString(),
        Description = "Shopper role for testing",
      });
      await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

      User user = CreateUser("eve", "hashed");
      await _repository.SaveAsync(user, TestContext.Current.CancellationToken);

      // Act
      User? result = await _repository.GetUserByUsernameAsync(
        Username.Create("eve"), TestContext.Current.CancellationToken);

      // Assert
      Assert.NotNull(result);
      Assert.Equal("eve", result.Username.Value);
    }

    [Fact(
      DisplayName = "Returns user case-insensitively")]
    public async Task GetUserByUsernameAsync_ReturnsUser_CaseInsensitively() {
      // Arrange
      _context.UserRoles.Add(new UserRoleDbEntity {
        Id = UserRoleIds.Shopper,
        Name = Role.Shopper.ToString(),
        Description = "Shopper role for testing",
      });
      await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

      User user = CreateUser("Frank", "hashed");
      await _repository.SaveAsync(user, TestContext.Current.CancellationToken);

      // Act
      User? result = await _repository.GetUserByUsernameAsync(
        Username.Create("FRANK"), TestContext.Current.CancellationToken);

      // Assert
      Assert.NotNull(result);
    }

    [Fact(
      DisplayName = "Returns correct role for user")]
    public async Task GetUserByUsernameAsync_ReturnsCorrectRole_ForUser() {
      // Arrange
      _context.UserRoles.Add(new UserRoleDbEntity {
        Id = UserRoleIds.Shopper,
        Name = Role.Shopper.ToString(),
        Description = "Shopper role for testing",
      });
      await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

      User user = CreateUser("grace", "hashed");
      await _repository.SaveAsync(user, TestContext.Current.CancellationToken);

      // Act
      User? result = await _repository.GetUserByUsernameAsync(
        Username.Create("grace"), TestContext.Current.CancellationToken);

      // Assert
      Assert.Equal(Role.Shopper, result!.Role);
    }

    [Fact(
      DisplayName = "Returns correct Uid for saved user")]
    public async Task GetUserByUsernameAsync_ReturnsCorrectUid_ForSavedUser() {
      // Arrange
      _context.UserRoles.Add(new UserRoleDbEntity {
        Id = UserRoleIds.Shopper,
        Name = Role.Shopper.ToString(),
        Description = "Shopper role for testing",
      });
      await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

      User user = CreateUser("henry", "hashed");
      Guid expectedUid = user.Id.Value;
      await _repository.SaveAsync(user, TestContext.Current.CancellationToken);

      // Act
      User? result = await _repository.GetUserByUsernameAsync(
        Username.Create("henry"), TestContext.Current.CancellationToken);

      // Assert
      Assert.Equal(expectedUid, result!.Id.Value);
    }
  }

  private static User CreateUser(string username, string passwordHash) =>
    User.Create(username, passwordHash);
}