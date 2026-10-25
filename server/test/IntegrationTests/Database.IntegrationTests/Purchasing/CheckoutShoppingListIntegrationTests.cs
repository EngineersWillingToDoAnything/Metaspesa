using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Shopping;
using Metaspesa.Database.Entities;
using Metaspesa.Database.Repositories;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping;
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
      clock,
      Substitute.For<IDomainEventPublisher>());
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


  [Fact]
  public async Task Handle_PersistsResetWithoutPriceSnapshot() {
    UserId ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, new ShoppingListName("Weekly"));
    list.AddItem(formatId, new PositiveAmount(2), true);
    await _shoppingRepository.SaveAsync(list, TestContext.Current.CancellationToken);

    await _handler.Handle(new CheckoutShoppingList.Command(
      ownerId.Value, list.Id.Value), TestContext.Current.CancellationToken);

    ShoppingList? actual = await _shoppingRepository.GetAsync(ownerId.Value,
      list.Id.Value, TestContext.Current.CancellationToken);
    Assert.False(actual!.Items.Single().IsChecked);
  }

  [Fact]
  public async Task Handle_DoesNotPersistPurchase() {
    UserId ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, new ShoppingListName("Weekly"));
    list.AddItem(formatId, new PositiveAmount(2), true);
    await _shoppingRepository.SaveAsync(list, TestContext.Current.CancellationToken);

    await _handler.Handle(new CheckoutShoppingList.Command(
      ownerId.Value, list.Id.Value), TestContext.Current.CancellationToken);

    Assert.Empty(await _context.Purchases.AsNoTracking()
      .ToArrayAsync(TestContext.Current.CancellationToken));
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
      UnitOfMeasure = "unit",
      ImageUrl = string.Empty,
    };
    _context.ProductFormats.Add(format);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    return new ProductFormatId(format.Id);
  }
}