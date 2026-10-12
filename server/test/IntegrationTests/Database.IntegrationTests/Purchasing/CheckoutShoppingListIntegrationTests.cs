using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Shopping;
using Metaspesa.Database.Entities;
using Metaspesa.Database.Repositories;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Metaspesa.Database.IntegrationTests.Purchasing;

[Collection("Database")]
public class CheckoutShoppingListIntegrationTests : IAsyncLifetime {
  private static readonly DateTime PurchasedAt =
    new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

  private readonly DatabaseFixture _fixture;
  private readonly MainContext _context;
  private readonly PostgreSqlShoppingListRepository _shoppingRepository;
  private readonly CheckoutShoppingList.Handler _handler;

  public CheckoutShoppingListIntegrationTests(DatabaseFixture fixture) {
    _fixture = fixture;
    _context = fixture.CreateContext();
    IClock clock = Substitute.For<IClock>();
    clock.GetCurrentTime().Returns(PurchasedAt);
    _shoppingRepository = new PostgreSqlShoppingListRepository(_context, clock);
    _handler = new CheckoutShoppingList.Handler(
      _shoppingRepository,
      new PostgreSqlPurchasePriceSnapshotReader(_context),
      new PostgreSqlPurchaseRepository(_context),
      clock);
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

  [Fact(DisplayName = "Checkout persists latest paid snapshot and resets checked items")]
  public async Task Handle_PersistsPurchaseAndReset_Atomically() {
    UserId ownerId = await SeedUserAsync();
    ProductFormatId milkId = await SeedFormatAsync("Milk");
    ProductFormatId breadId = await SeedFormatAsync("Bread");
    await SeedSnapshotAsync(milkId, PurchasedAt.AddDays(-2), 1.25m);
    PriceSnapshotId latestMilk = await SeedSnapshotAsync(
      milkId, PurchasedAt.AddDays(-1), 1.50m);
    await SeedSnapshotAsync(breadId, PurchasedAt.AddDays(-1), 2.00m);
    var list = ShoppingList.Create(ownerId, new ShoppingListName("Weekly"));
    list.AddItem(milkId, new PositiveAmount(2), true);
    list.AddItem(breadId, new PositiveAmount(3), false);
    Guid listId = await _shoppingRepository.AddAsync(
      list, TestContext.Current.CancellationToken);

    await _handler.Handle(
      new CheckoutShoppingList.Command(ownerId.Value, listId),
      TestContext.Current.CancellationToken);

    PurchaseDbEntity purchase = await _context.Purchases
      .AsNoTracking()
      .Include(value => value.Items)
      .SingleAsync(
        value => value.UserUid == ownerId.Value,
        TestContext.Current.CancellationToken);
    PurchaseItemDbEntity item = Assert.Single(purchase.Items);
    Assert.Equal(latestMilk.Value, item.PriceSnapshotId);
    Assert.Equal(2, item.Amount);
    Assert.Equal(PurchasedAt, purchase.PurchasedAt);
    ShoppingList reset = Assert.IsType<ShoppingList>(await _shoppingRepository.GetAsync(
      ownerId,
      new ShoppingListId(listId),
      TestContext.Current.CancellationToken));
    Assert.All(reset.Items, value => Assert.False(value.IsChecked));
  }

  [Fact(DisplayName = "Missing snapshot persists neither purchase nor reset")]
  public async Task Handle_LeavesDatabaseUnchanged_WhenSnapshotIsMissing() {
    UserId ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, new ShoppingListName("Weekly"));
    list.AddItem(formatId, new PositiveAmount(2), true);
    Guid listId = await _shoppingRepository.AddAsync(
      list, TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<PurchasePriceSnapshotNotFoundException>(() =>
      _handler.Handle(
        new CheckoutShoppingList.Command(ownerId.Value, listId),
        TestContext.Current.CancellationToken));

    Assert.Empty(await _context.Purchases
      .AsNoTracking()
      .ToListAsync(TestContext.Current.CancellationToken));
    ShoppingList unchanged = Assert.IsType<ShoppingList>(await _shoppingRepository.GetAsync(
      ownerId,
      new ShoppingListId(listId),
      TestContext.Current.CancellationToken));
    Assert.True(unchanged.Items.Single().IsChecked);
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

  private async Task<ProductFormatId> SeedFormatAsync(string name) {
    var format = new ProductFormatDbEntity {
      Id = Uid.Create(),
      Product = new ProductDbEntity {
        Id = Uid.Create(),
        Name = name,
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
    _context.ProductFormats.Add(format);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    return new ProductFormatId(format.Id);
  }

  private async Task<PriceSnapshotId> SeedSnapshotAsync(
    ProductFormatId formatId,
    DateTime observedAt,
    decimal price
  ) {
    var snapshot = new PriceSnapshotDbEntity {
      Id = Uid.Create(),
      ProductFormatId = formatId.Value,
      PriceAmount = price,
      ObservedAt = observedAt,
    };
    _context.PriceSnapshots.Add(snapshot);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    return new PriceSnapshotId(snapshot.Id);
  }
}