using Metaspesa.Database.Entities;
using Metaspesa.Database.Repositories;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping;
using Microsoft.EntityFrameworkCore;

namespace Metaspesa.Database.IntegrationTests.Purchasing;

[Collection("Database")]
public class PostgreSqlPurchaseRepositoryTests : IAsyncLifetime {
  private static readonly DateTime PurchasedAt =
    new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

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
    await EnsureShopperRoleAsync();
  }

  public async ValueTask DisposeAsync() {
    await _context.DisposeAsync();
    GC.SuppressFinalize(this);
  }

  [Fact(DisplayName = "Persists immutable purchase header and lines")]
  public async Task Add_PersistsTypedPurchase_WithManyItems() {
    UserId buyerId = await SeedUserAsync();
    ShoppingListId listId = await SeedShoppingListAsync(buyerId);
    PriceSnapshotId firstSnapshotId = await SeedSnapshotAsync("Milk", 1.25m);
    PriceSnapshotId secondSnapshotId = await SeedSnapshotAsync("Bread", 2.50m);
    var purchase = Purchase.Create(
      buyerId,
      listId,
      [
        new PurchaseItem(firstSnapshotId, new PositiveAmount(2)),
        new PurchaseItem(secondSnapshotId, new PositiveAmount(3)),
      ],
      PurchasedAt);

    await _repository.AddAsync(
      purchase, TestContext.Current.CancellationToken);

    PurchaseDbEntity entity = await _context.Purchases
      .AsNoTracking()
      .Include(value => value.Items)
      .SingleAsync(
        value => value.UserUid == buyerId.Value,
        TestContext.Current.CancellationToken);
    Assert.Equal(listId.Value, entity.ShoppingListId);
    Assert.Equal(PurchasedAt, entity.PurchasedAt);
    var itemsBySnapshotId = entity.Items.ToDictionary(item => item.PriceSnapshotId);
    Assert.Equal(2, itemsBySnapshotId[firstSnapshotId.Value].Amount);
    Assert.Equal(3, itemsBySnapshotId[secondSnapshotId.Value].Amount);
  }

  [Fact(DisplayName = "Persists purchase with nullable external references")]
  public async Task Add_PersistsNullBuyerAndList() {
    PriceSnapshotId snapshotId = await SeedSnapshotAsync("Milk", 1.25m);
    var purchase = Purchase.Create(
      null,
      null,
      [new PurchaseItem(snapshotId, new PositiveAmount(1))],
      PurchasedAt);

    await _repository.AddAsync(
      purchase, TestContext.Current.CancellationToken);

    PurchaseDbEntity entity = await _context.Purchases
      .AsNoTracking()
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Null(entity.UserUid);
    Assert.Null(entity.ShoppingListId);
  }

  [Fact(DisplayName = "Retains purchase after buyer and list deletion")]
  public async Task Persistence_SetsExternalReferencesNull_WhenSourcesAreDeleted() {
    UserId buyerId = await SeedUserAsync();
    ShoppingListId listId = await SeedShoppingListAsync(buyerId);
    PriceSnapshotId snapshotId = await SeedSnapshotAsync("Milk", 1.25m);
    await _repository.AddAsync(Purchase.Create(
      buyerId,
      listId,
      [new PurchaseItem(snapshotId, new PositiveAmount(1))],
      PurchasedAt), TestContext.Current.CancellationToken);

    await _context.Users
      .Where(user => user.Uid == buyerId.Value)
      .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
    await _context.ShoppingLists
      .Where(list => list.Id == listId.Value)
      .ExecuteDeleteAsync(TestContext.Current.CancellationToken);

    PurchaseDbEntity entity = await _context.Purchases
      .AsNoTracking()
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Null(entity.UserUid);
    Assert.Null(entity.ShoppingListId);
    Assert.Single(await _context.PurchaseItems
      .AsNoTracking()
      .ToListAsync(TestContext.Current.CancellationToken));
  }

  private async Task EnsureShopperRoleAsync() {
    if (await _context.UserRoles.AnyAsync(
      role => role.Id == UserRoleIds.Shopper,
      TestContext.Current.CancellationToken)) {
      return;
    }

    _context.UserRoles.Add(new UserRoleDbEntity {
      Id = UserRoleIds.Shopper,
      Name = nameof(Role.Shopper),
      Description = "Regular user who manages shopping lists",
    });
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
  }

  private async Task<UserId> SeedUserAsync() {
    var id = new UserId(Guid.CreateVersion7());
    _context.Users.Add(new UserDbEntity {
      Uid = id.Value,
      Username = id.ToString(),
      EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    return id;
  }

  private async Task<ShoppingListId> SeedShoppingListAsync(UserId ownerId) {
    var entity = new ShoppingListDbEntity {
      Id = Uid.Create(),
      Name = $"List {Guid.CreateVersion7()}",
      IsTemporary = false,
      Ownerships = [new ShoppingListOwnershipDbEntity {
        UserUid = ownerId.Value,
      }],
    };
    _context.ShoppingLists.Add(entity);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    return new ShoppingListId(entity.Id);
  }

  private async Task<PriceSnapshotId> SeedSnapshotAsync(
    string productName, decimal price
  ) {
    var format = new ProductFormatDbEntity {
      Id = Uid.Create(),
      Product = new ProductDbEntity {
        Id = Uid.Create(),
        Name = productName,
        SuperMarket = new SuperMarketDbEntity {
          Id = Uid.Create(),
          Name = $"Market {Guid.CreateVersion7()}",
        },
        Brand = new ProductBrandDbEntity {
          Id = Uid.Create(),
          Name = $"Brand {Guid.CreateVersion7()}",
        },
      },
      Quantity = 1,
      UnitOfMeasure = new UnitOfMeasureDbEntity {
        Id = Uid.Create(),
        Code = $"u{Guid.CreateVersion7():N}"[..16],
        Name = $"Unit {Guid.CreateVersion7()}",
      },
      ImageUrl = string.Empty,
    };
    var snapshot = new PriceSnapshotDbEntity {
      Id = Uid.Create(),
      ProductFormat = format,
      PriceAmount = price,
      ObservedAt = PurchasedAt.AddDays(-1),
    };
    _context.PriceSnapshots.Add(snapshot);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    return new PriceSnapshotId(snapshot.Id);
  }
}