using Metaspesa.Database.Entities;
using Metaspesa.Database.Repositories;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Shopping;
using Microsoft.EntityFrameworkCore;

namespace Metaspesa.Database.IntegrationTests.Purchasing;

[Collection("Database")]
public class PostgreSqlPurchaseRepositoryTests : IAsyncLifetime {
  private readonly DatabaseFixture _fixture;
  private readonly MainContext _context;
  private readonly PostgreSqlPurchaseRepository _repository;

  public PostgreSqlPurchaseRepositoryTests(DatabaseFixture fixture) {
    _fixture = fixture;
    _context = fixture.CreateContext();
    _repository = new PostgreSqlPurchaseRepository(_context);
  }

  public async ValueTask InitializeAsync() {
    CancellationToken cancellationToken = TestContext.Current.CancellationToken;
    await _fixture.DeleteShoppingProductReferencesAsync(cancellationToken);
    await _context.PriceSnapshots.ExecuteDeleteAsync(cancellationToken);
    await _context.ProductFormats.ExecuteDeleteAsync(cancellationToken);
    await _context.Products.ExecuteDeleteAsync(cancellationToken);
    await _context.ProductBrands.ExecuteDeleteAsync(cancellationToken);
    await _context.SuperMarkets.ExecuteDeleteAsync(cancellationToken);
    if (!await _context.UserRoles.AnyAsync(
      role => role.Id == UserRoleIds.Shopper, cancellationToken)) {
      _context.UserRoles.Add(new UserRoleDbEntity {
        Id = UserRoleIds.Shopper, Name = nameof(Role.Shopper),
        Description = "Regular user who manages shopping lists",
      });
      await _context.SaveChangesAsync(cancellationToken);
    }
  }

  public async ValueTask DisposeAsync() {
    await _context.DisposeAsync();
    GC.SuppressFinalize(this);
  }

  [Fact]
  public async Task SaveAsync_PersistsPurchaseId() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var purchase = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));

    await _repository.SaveAsync(purchase, TestContext.Current.CancellationToken);

    PurchaseDbEntity entity = await _context.Purchases.AsNoTracking()
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal(purchase.Id.Value, entity.Id);
  }

  [Fact]
  public async Task SaveAsync_PersistsBuyerId() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var purchase = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));

    await _repository.SaveAsync(purchase, TestContext.Current.CancellationToken);

    PurchaseDbEntity entity = await _context.Purchases.AsNoTracking()
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal(buyerId, entity.UserUid);
  }

  [Fact]
  public async Task SaveAsync_PersistsListId() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var purchase = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));

    await _repository.SaveAsync(purchase, TestContext.Current.CancellationToken);

    PurchaseDbEntity entity = await _context.Purchases.AsNoTracking()
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal(listId, entity.ShoppingListId);
  }

  [Fact]
  public async Task SaveAsync_PersistsTimestamp() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var purchase = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));

    await _repository.SaveAsync(purchase, TestContext.Current.CancellationToken);

    PurchaseDbEntity entity = await _context.Purchases.AsNoTracking()
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal(purchasedAt, entity.PurchasedAt);
  }

  [Fact]
  public async Task SaveAsync_PersistsAllItemIds() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var purchase = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));

    await _repository.SaveAsync(purchase, TestContext.Current.CancellationToken);

    Guid[] actual = await _context.PurchaseItems.AsNoTracking()
      .Select(item => item.Id).ToArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal(purchase.Items.Select(item => item.Id.Value).Order(), actual.Order());
  }

  [Fact]
  public async Task SaveAsync_PersistsItemSnapshots() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var purchase = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));

    await _repository.SaveAsync(purchase, TestContext.Current.CancellationToken);

    Guid[] actual = await _context.PurchaseItems.AsNoTracking()
      .Select(item => item.PriceSnapshotId).ToArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal(purchase.Items.Select(item => item.PriceSnapshotId.Value).Order(), actual.Order());
  }

  [Fact]
  public async Task SaveAsync_PersistsItemAmounts() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var purchase = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));

    await _repository.SaveAsync(purchase, TestContext.Current.CancellationToken);

    int[] actual = await _context.PurchaseItems.AsNoTracking()
      .Select(item => item.Amount).ToArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal(purchase.Items.Select(item => item.Amount.Value).Order(), actual.Order());
  }

  [Fact]
  public async Task SaveAsync_UpdatesExistingRoot() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var original = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));
    await _repository.SaveAsync(original, TestContext.Current.CancellationToken);
    var replacement = Purchase.Create(new CreatePurchaseParams(
      original.Id.Value, buyerId, listId, purchasedAt.AddHours(1), [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));

    await _repository.SaveAsync(replacement, TestContext.Current.CancellationToken);

    DateTime actual = await _context.Purchases.AsNoTracking()
      .Select(purchase => purchase.PurchasedAt)
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal(replacement.PurchasedAt, actual);
  }

  [Fact]
  public async Task SaveAsync_RemovesItemsAbsentFromAggregate() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var original = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));
    await _repository.SaveAsync(original, TestContext.Current.CancellationToken);
    var replacement = Purchase.Create(new CreatePurchaseParams(
      original.Id.Value, buyerId, listId, purchasedAt,
      [new CreatePurchaseItem(snapshots[0].Id, 2)]));

    await _repository.SaveAsync(replacement, TestContext.Current.CancellationToken);

    Guid[] actual = await _context.PurchaseItems.AsNoTracking()
      .Select(item => item.PriceSnapshotId)
      .ToArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal([snapshots[0].Id], actual);
  }

  [Fact]
  public async Task SaveAsync_PersistsReplacementItemState() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var original = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));
    await _repository.SaveAsync(original, TestContext.Current.CancellationToken);
    var replacement = Purchase.Create(new CreatePurchaseParams(
      original.Id.Value, buyerId, listId, purchasedAt,
      [new CreatePurchaseItem(snapshots[0].Id, 9)]));

    await _repository.SaveAsync(replacement, TestContext.Current.CancellationToken);

    int actual = await _context.PurchaseItems.AsNoTracking()
      .Select(item => item.Amount).SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal(9, actual);
  }

  [Fact]
  public async Task SaveAsync_AddsNewItems() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var original = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));
    await _repository.SaveAsync(original, TestContext.Current.CancellationToken);
    var addition = new PriceSnapshotDbEntity {
      Id = Guid.CreateVersion7(), ProductFormatId = snapshots[0].ProductFormatId,
      PriceAmount = 3m, ObservedAt = purchasedAt,
    };
    _context.PriceSnapshots.Add(addition);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var replacement = Purchase.Create(new CreatePurchaseParams(
      original.Id.Value, buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
        new CreatePurchaseItem(addition.Id, 4),
      ]));

    await _repository.SaveAsync(replacement, TestContext.Current.CancellationToken);

    Guid[] actual = await _context.PurchaseItems.AsNoTracking()
      .Select(item => item.PriceSnapshotId)
      .ToArrayAsync(TestContext.Current.CancellationToken);
    Assert.Contains(addition.Id, actual);
  }

  [Fact]
  public async Task SaveAsync_RepeatedSaveDoesNotDuplicateLines() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var purchase = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));
    await _repository.SaveAsync(purchase, TestContext.Current.CancellationToken);

    await _repository.SaveAsync(purchase, TestContext.Current.CancellationToken);

    Assert.Equal(2, await _context.PurchaseItems.CountAsync(
      TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task SaveAsync_PersistsNullBuyerReference() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var original = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));
    await _repository.SaveAsync(original, TestContext.Current.CancellationToken);
    await _context.Users
      .Where(row => row.Uid == buyerId)
      .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
    Purchase? purchase = await _repository.GetAsync(original.Id.Value,
      TestContext.Current.CancellationToken);

    await _repository.SaveAsync(purchase!, TestContext.Current.CancellationToken);

    PurchaseDbEntity entity = await _context.Purchases.AsNoTracking()
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Null(entity.UserUid);
  }

  [Fact]
  public async Task SaveAsync_PersistsNullListReference() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var original = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));
    await _repository.SaveAsync(original, TestContext.Current.CancellationToken);
    await _context.ShoppingLists
      .Where(row => row.Id == listId)
      .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
    Purchase? purchase = await _repository.GetAsync(original.Id.Value,
      TestContext.Current.CancellationToken);

    await _repository.SaveAsync(purchase!, TestContext.Current.CancellationToken);

    PurchaseDbEntity entity = await _context.Purchases.AsNoTracking()
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Null(entity.ShoppingListId);
  }

  [Fact]
  public async Task GetAsync_RehydratesPersistedItemIds() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var purchase = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));
    await _repository.SaveAsync(purchase, TestContext.Current.CancellationToken);

    Purchase? actual = await _repository.GetAsync(purchase.Id.Value,
      TestContext.Current.CancellationToken);

    Assert.Equal(purchase.Items.Select(item => item.Id.Value).Order(),
      actual!.Items.Select(item => item.Id.Value).Order());
  }

  [Fact]
  public async Task GetAsync_ReturnsNull_WhenPurchaseIsMissing() {
    Purchase? actual = await _repository.GetAsync(
      Guid.Parse("00000000-0000-7000-8000-000000000001"),
      TestContext.Current.CancellationToken);

    Assert.Null(actual);
  }

  [Fact]
  public async Task SaveAsync_RollsBackHeader_WhenChildInsertFails() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var original = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));
    await _repository.SaveAsync(original, TestContext.Current.CancellationToken);
    var replacement = Purchase.Create(new CreatePurchaseParams(
      original.Id.Value, buyerId, listId, purchasedAt.AddHours(1),
      [new CreatePurchaseItem(
        Guid.Parse("00000000-0000-7000-8000-000000000001"), 9)]));

    _ = await Record.ExceptionAsync(() => _repository.SaveAsync(
      replacement, TestContext.Current.CancellationToken));

    DateTime actual = await _context.Purchases.AsNoTracking()
      .Select(purchase => purchase.PurchasedAt)
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal(purchasedAt, actual);
  }

  [Fact]
  public async Task SaveAsync_RollsBackItems_WhenChildInsertFails() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var original = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));
    await _repository.SaveAsync(original, TestContext.Current.CancellationToken);
    var replacement = Purchase.Create(new CreatePurchaseParams(
      original.Id.Value, buyerId, listId, purchasedAt.AddHours(1),
      [new CreatePurchaseItem(
        Guid.Parse("00000000-0000-7000-8000-000000000001"), 9)]));

    _ = await Record.ExceptionAsync(() => _repository.SaveAsync(
      replacement, TestContext.Current.CancellationToken));

    Guid[] actual = await _context.PurchaseItems.AsNoTracking()
      .Select(item => item.Id).ToArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal(original.Items.Select(item => item.Id.Value).Order(), actual.Order());
  }

  [Fact]
  public async Task SaveAsync_DoesNotPersistRoot_WhenChildInsertFails() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var original = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));
    var invalid = Purchase.Create(new CreatePurchaseParams(
      original.Id.Value, buyerId, listId, purchasedAt,
      [new CreatePurchaseItem(
        Guid.Parse("00000000-0000-7000-8000-000000000001"), 1)]));

    _ = await Record.ExceptionAsync(() => _repository.SaveAsync(
      invalid, TestContext.Current.CancellationToken));

    Assert.Empty(await _context.Purchases.AsNoTracking()
      .ToArrayAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task SaveAsync_PropagatesCancellation() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var purchase = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));
    using var source = new CancellationTokenSource();
    await source.CancelAsync();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
      _repository.SaveAsync(purchase, source.Token));
  }

  [Fact]
  public async Task SaveAsync_RejectsNullAggregate() {
    await Assert.ThrowsAsync<ArgumentNullException>(() =>
      _repository.SaveAsync(null!, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Persistence_RetainsReceipt_WhenBuyerIsDeleted() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var purchase = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));
    await _repository.SaveAsync(purchase, TestContext.Current.CancellationToken);

    await _context.Users
      .Where(row => row.Uid == buyerId)
      .ExecuteDeleteAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, await _context.PurchaseItems.AsNoTracking()
      .CountAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Persistence_RetainsReceipt_WhenListIsDeleted() {
    var buyerId = Guid.CreateVersion7();
    var listId = Guid.CreateVersion7();
    var purchasedAt = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    _context.Users.Add(new UserDbEntity {
      Uid = buyerId, Username = buyerId.ToString(), EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    _context.ShoppingLists.Add(new ShoppingListDbEntity {
      Id = listId, Name = "Weekly",
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = buyerId }],
    });
    var snapshots = new List<PriceSnapshotDbEntity>();
    foreach (string name in new[] { "Milk", "Bread" }) {
      var snapshot = new PriceSnapshotDbEntity {
        Id = Guid.CreateVersion7(), PriceAmount = 1.25m,
        ObservedAt = purchasedAt.AddDays(-1),
        ProductFormat = new ProductFormatDbEntity {
          Id = Guid.CreateVersion7(), Quantity = 1, ImageUrl = string.Empty,
          UnitOfMeasure = new UnitOfMeasureDbEntity {
            Id = Guid.CreateVersion7(), Code = $"u{Guid.NewGuid():N}"[..16],
            Name = "Unit",
          },
          Product = new ProductDbEntity {
            Id = Guid.CreateVersion7(), Name = name,
            Brand = new ProductBrandDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Brand {Guid.CreateVersion7()}",
            },
            SuperMarket = new SuperMarketDbEntity {
              Id = Guid.CreateVersion7(), Name = $"Market {Guid.CreateVersion7()}",
            },
          },
        },
      };
      snapshots.Add(snapshot);
      _context.PriceSnapshots.Add(snapshot);
    }
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var purchase = Purchase.Create(new CreatePurchaseParams(
      Guid.CreateVersion7(), buyerId, listId, purchasedAt, [
        new CreatePurchaseItem(snapshots[0].Id, 2),
        new CreatePurchaseItem(snapshots[1].Id, 3),
      ]));
    await _repository.SaveAsync(purchase, TestContext.Current.CancellationToken);

    await _context.ShoppingLists
      .Where(row => row.Id == listId)
      .ExecuteDeleteAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, await _context.PurchaseItems.AsNoTracking()
      .CountAsync(TestContext.Current.CancellationToken));
  }
}