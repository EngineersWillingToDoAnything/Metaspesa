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

namespace Metaspesa.Database.IntegrationTests.Shopping;

[Collection("Database")]
public class PostgreSqlShoppingListRepositoryTests : IAsyncLifetime {
  private static readonly DateTime RemovedAt =
    new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);

  private readonly MainContext _context;
  private readonly PostgreSqlShoppingListRepository _repository;

  public PostgreSqlShoppingListRepositoryTests(DatabaseFixture fixture) {
    _context = fixture.CreateContext();
    IClock clock = Substitute.For<IClock>();
    clock.GetCurrentTime().Returns(RemovedAt);
    _repository = new PostgreSqlShoppingListRepository(_context, clock);
  }

  public async ValueTask InitializeAsync() {
    if (!await _context.UserRoles.AnyAsync(
      role => role.Id == UserRoleIds.Shopper,
      TestContext.Current.CancellationToken)) {
      _context.UserRoles.Add(new UserRoleDbEntity {
        Id = UserRoleIds.Shopper,
        Name = nameof(Role.Shopper),
        Description = "Regular user who manages shopping lists",
      });
      await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
  }

  public async ValueTask DisposeAsync() {
    await _context.DisposeAsync();
    GC.SuppressFinalize(this);
  }

  [Fact(DisplayName = "Persists and reconstructs named aggregate")]
  public async Task AddAndGetAsync_PersistsTypedAggregate() {
    UserId ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedProductFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, new ShoppingListName("Weekly"));
    list.AddItem(formatId, new PositiveAmount(2), true);

    Guid id = await _repository.AddAsync(list, TestContext.Current.CancellationToken);
    ShoppingList? result = await _repository.GetAsync(
      ownerId,
      new ShoppingListId(id),
      TestContext.Current.CancellationToken);

    Assert.NotNull(result);
    Assert.Equal(new ShoppingListName("Weekly"), result.Name);
    Assert.False(result.IsTemporary);
    Assert.Equal(ownerId, Assert.Single(result.OwnerIds));
    ShoppingItem item = Assert.Single(result.Items);
    Assert.Equal(formatId, item.ProductFormatId);
    Assert.Equal(2, item.Amount.Value);
    Assert.True(item.IsChecked);
  }

  [Fact(DisplayName = "Round trips shopping item identity")]
  public async Task SaveAndGetAsync_PreservesShoppingItemId() {
    UserId ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedProductFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, new ShoppingListName("Weekly"));
    list.AddItem(formatId, new PositiveAmount(2), true);
    ShoppingItem originalItem = Assert.Single(list.Items);

    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);
    ShoppingList result = Assert.IsType<ShoppingList>(
      await _repository.GetAsync(ownerId.Value, list.Id.Value,
        TestContext.Current.CancellationToken));

    Assert.Equal(originalItem.Id, Assert.Single(result.Items).Id);
  }

  [Fact(DisplayName = "Round trips shopping item deletion timestamp")]
  public async Task SaveAndGetAsync_PreservesShoppingItemDeletedAt() {
    UserId ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedProductFormatAsync("Milk");
    var item = ShoppingItem.Rehydrate(
      Guid.CreateVersion7(), formatId.Value, 2, true, RemovedAt);
    var list = ShoppingList.Rehydrate(
      new ShoppingListId(Guid.CreateVersion7()),
      [ownerId],
      new ShoppingListName("Weekly"),
      null,
      [item]);

    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);
    ShoppingList result = Assert.IsType<ShoppingList>(
      await _repository.GetAsync(ownerId.Value, list.Id.Value,
        TestContext.Current.CancellationToken));

    Assert.Equal(RemovedAt, Assert.Single(result.Items).DeletedAt);
  }

  [Fact(DisplayName = "Persists temporary empty aggregate")]
  public async Task AddAndGetAsync_PersistsTemporaryEmptyList() {
    UserId ownerId = await SeedUserAsync();
    Guid id = await _repository.AddAsync(ShoppingList.Create(ownerId, null),
      TestContext.Current.CancellationToken);

    ShoppingList? result = await _repository.GetAsync(
      ownerId, new ShoppingListId(id), TestContext.Current.CancellationToken);

    Assert.NotNull(result);
    Assert.True(result.IsTemporary);
    Assert.Null(result.Name);
    Assert.Empty(result.Items);
  }

  [Fact(DisplayName = "Returns only owner-accessible lists")]
  public async Task GetByOwnerAsync_IsolatesOwners() {
    UserId ownerId = await SeedUserAsync();
    UserId otherOwnerId = await SeedUserAsync();
    await _repository.AddAsync(ShoppingList.Create(ownerId,
      new ShoppingListName("Mine")), TestContext.Current.CancellationToken);
    await _repository.AddAsync(ShoppingList.Create(otherOwnerId,
      new ShoppingListName("Other")), TestContext.Current.CancellationToken);

    IReadOnlyCollection<ShoppingList> result = await _repository.GetByOwnerAsync(
      ownerId, TestContext.Current.CancellationToken);

    ShoppingList list = Assert.Single(result);
    Assert.Equal(new ShoppingListName("Mine"), list.Name);
  }

  [Fact(DisplayName = "Does not load another owner's matching list")]
  public async Task GetAsync_IsolatesOwners() {
    UserId ownerId = await SeedUserAsync();
    UserId otherOwnerId = await SeedUserAsync();
    Guid id = await _repository.AddAsync(ShoppingList.Create(
      otherOwnerId, new ShoppingListName("Weekly")),
      TestContext.Current.CancellationToken);

    ShoppingList? result = await _repository.GetAsync(
      ownerId,
      new ShoppingListId(id),
      TestContext.Current.CancellationToken);

    Assert.Null(result);
  }

  [Fact(DisplayName = "Created list ID resolves only for its owner")]
  public async Task GetAsync_ByIdIsolatesOwners() {
    UserId ownerId = await SeedUserAsync();
    UserId otherOwnerId = await SeedUserAsync();
    Guid id = await _repository.AddAsync(ShoppingList.Create(ownerId,
      new ShoppingListName("Rest list")), TestContext.Current.CancellationToken);

    ShoppingList? result = await _repository.GetAsync(
      otherOwnerId, new ShoppingListId(id), TestContext.Current.CancellationToken);

    Assert.Null(result);
  }

  [Fact(DisplayName = "Create returns persisted shopping list ID")]
  public async Task AddAsync_ReturnsPersistedId() {
    UserId ownerId = await SeedUserAsync();
    Guid id = await _repository.AddAsync(ShoppingList.Create(ownerId,
      new ShoppingListName("Rest list")), TestContext.Current.CancellationToken);

    ShoppingList? result = await _repository.GetAsync(
      ownerId, new ShoppingListId(id), TestContext.Current.CancellationToken);

    Assert.NotNull(result);
    Assert.Equal(id, result.Id.Value);
  }

  [Fact(DisplayName = "Orders named lists before temporary list")]
  public async Task GetByOwnerAsync_OrdersNamedListsBeforeTemporaryList() {
    UserId ownerId = await SeedUserAsync();
    await _repository.AddAsync(ShoppingList.Create(ownerId, null),
      TestContext.Current.CancellationToken);
    await _repository.AddAsync(ShoppingList.Create(ownerId,
      new ShoppingListName("Weekly")), TestContext.Current.CancellationToken);

    IReadOnlyCollection<ShoppingList> result = await _repository.GetByOwnerAsync(
      ownerId, TestContext.Current.CancellationToken);

    Assert.Collection(
      result,
      list => Assert.Equal(new ShoppingListName("Weekly"), list.Name),
      list => Assert.True(list.IsTemporary));
  }

  [Fact(DisplayName = "Doesn't find named list case-insensitively")]
  public async Task ExistsAsync_DoesNotMatchNamedList_WithDifferentCase() {
    UserId ownerId = await SeedUserAsync();
    await _repository.AddAsync(ShoppingList.Create(ownerId,
      new ShoppingListName("Weekly")), TestContext.Current.CancellationToken);

    bool exists = await _repository.ExistsAsync(
      ownerId.Value,
      "WEEKLY",
      TestContext.Current.CancellationToken);

    Assert.False(exists);
  }

  [Fact(DisplayName = "Finds named list trimming name")]
  public async Task ExistsAsync_MatchesNamedList_TrimmingName() {
    UserId ownerId = await SeedUserAsync();
    await _repository.AddAsync(ShoppingList.Create(ownerId,
      new ShoppingListName("Weekly")), TestContext.Current.CancellationToken);

    bool exists = await _repository.ExistsAsync(
      ownerId.Value,
      "   Weekly   ",
      TestContext.Current.CancellationToken);

    Assert.True(exists);
  }

  [Fact(DisplayName = "Does not find another owner's matching list")]
  public async Task ExistsAsync_IsolatesOwners() {
    UserId ownerId = await SeedUserAsync();
    UserId otherOwnerId = await SeedUserAsync();
    await _repository.AddAsync(ShoppingList.Create(otherOwnerId, null),
      TestContext.Current.CancellationToken);

    bool exists = await _repository.ExistsAsync(
      ownerId.Value, null, TestContext.Current.CancellationToken);

    Assert.False(exists);
  }

  [Fact(DisplayName = "Saves complete aggregate snapshot")]
  public async Task SaveAsync_PersistsCompleteAggregateSnapshot() {
    UserId ownerId = await SeedUserAsync();
    ProductFormatId milkId = await SeedProductFormatAsync("Milk");
    ProductFormatId breadId = await SeedProductFormatAsync("Bread");
    var list = ShoppingList.Create(ownerId, new ShoppingListName("Weekly"));
    list.AddItem(milkId, new PositiveAmount(2), true);
    list.AddItem(breadId, new PositiveAmount(1), false);

    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);

    var persisted = new {
      List = await _context.ShoppingLists
        .AsNoTracking()
        .Where(row => row.Id == list.Id.Value)
        .Select(row => new {
          row.Id,
          row.Name,
          row.IsTemporary,
          row.DeletedAt,
        })
        .SingleAsync(TestContext.Current.CancellationToken),
      Owners = await _context.ShoppingListOwnerships
        .AsNoTracking()
        .Where(row => row.ShoppingListId == list.Id.Value)
        .Select(row => row.UserUid)
        .OrderBy(id => id)
        .ToArrayAsync(TestContext.Current.CancellationToken),
      Items = await _context.ShoppingItems
        .AsNoTracking()
        .Where(row => row.ShoppingListId == list.Id.Value)
        .OrderBy(row => row.ProductFormatId)
        .Select(row => new {
          row.ProductFormatId,
          row.Amount,
          row.IsChecked,
          row.DeletedAt,
        })
        .ToArrayAsync(TestContext.Current.CancellationToken),
    };
    var expected = new {
      List = new {
        Id = list.Id.Value,
        Name = "Weekly",
        IsTemporary = false,
        DeletedAt = (DateTime?)null,
      },
      Owners = new[] { ownerId.Value },
      Items = new[] {
        new {
          ProductFormatId = milkId.Value,
          Amount = 2,
          IsChecked = true,
          DeletedAt = (DateTime?)null,
        },
        new {
          ProductFormatId = breadId.Value,
          Amount = 1,
          IsChecked = false,
          DeletedAt = (DateTime?)null,
        },
      }.OrderBy(item => item.ProductFormatId).ToArray(),
    };

    Assert.Equivalent(expected, persisted, strict: true);
  }

  [Fact(DisplayName = "Updates existing root without creating another root")]
  public async Task SaveAsync_UpdatesExistingRoot() {
    UserId ownerId = await SeedUserAsync();
    var list = ShoppingList.Create(ownerId, new ShoppingListName("Original"));
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);
    list.Update("Renamed");

    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);

    string?[] persistedNames = await _context.ShoppingLists
      .AsNoTracking()
      .Where(row => row.Id == list.Id.Value)
      .Select(row => row.Name)
      .ToArrayAsync(TestContext.Current.CancellationToken);
    string?[] expectedNames = ["Renamed"];
    Assert.Equal(expectedNames, persistedNames);
  }

  [Fact(DisplayName = "Physically removes ownership absent from aggregate")]
  public async Task SaveAsync_RemovesOwnershipAbsentFromAggregate() {
    UserId retainedOwnerId = await SeedUserAsync();
    UserId removedOwnerId = await SeedUserAsync();
    var original = ShoppingList.Rehydrate(
      new ShoppingListId(Uid.Create()),
      [retainedOwnerId, removedOwnerId],
      new ShoppingListName("Shared"),
      null,
      []);
    await _repository.SaveAsync(original, TestContext.Current.CancellationToken);
    var replacement = ShoppingList.Rehydrate(
      original.Id,
      [retainedOwnerId],
      original.Name,
      original.DeletedAt,
      original.Items);

    await _repository.SaveAsync(replacement, TestContext.Current.CancellationToken);

    Guid[] persistedOwners = await _context.ShoppingListOwnerships
      .AsNoTracking()
      .Where(row => row.ShoppingListId == original.Id.Value)
      .Select(row => row.UserUid)
      .ToArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal(new[] { retainedOwnerId.Value }, persistedOwners);
  }

  [Fact(DisplayName = "Inserts ownership added to aggregate")]
  public async Task SaveAsync_InsertsOwnershipAddedToAggregate() {
    UserId originalOwnerId = await SeedUserAsync();
    UserId addedOwnerId = await SeedUserAsync();
    var original = ShoppingList.Create(
      originalOwnerId, new ShoppingListName("Shared"));
    await _repository.SaveAsync(original, TestContext.Current.CancellationToken);
    var replacement = ShoppingList.Rehydrate(
      original.Id,
      [originalOwnerId, addedOwnerId],
      original.Name,
      original.DeletedAt,
      original.Items);

    await _repository.SaveAsync(replacement, TestContext.Current.CancellationToken);

    Guid[] persistedOwners = await _context.ShoppingListOwnerships
      .AsNoTracking()
      .Where(row => row.ShoppingListId == original.Id.Value)
      .Select(row => row.UserUid)
      .OrderBy(id => id)
      .ToArrayAsync(TestContext.Current.CancellationToken);
    Guid[] expectedOwners =
      [.. new[] { originalOwnerId.Value, addedOwnerId.Value }.Order()];
    Assert.Equal(expectedOwners, persistedOwners);
  }

  [Fact(DisplayName = "Physically removes item absent from aggregate")]
  public async Task SaveAsync_RemovesItemAbsentFromAggregate() {
    UserId ownerId = await SeedUserAsync();
    ProductFormatId retainedFormatId = await SeedProductFormatAsync("Bread");
    ProductFormatId removedFormatId = await SeedProductFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, new ShoppingListName("Weekly"));
    list.AddItem(retainedFormatId, new PositiveAmount(1), false);
    list.AddItem(removedFormatId, new PositiveAmount(1), false);
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);
    ShoppingItem retainedItem = list.Items.Single(
      item => item.ProductFormatId == retainedFormatId);
    var replacement = ShoppingList.Rehydrate(
      list.Id, list.OwnerIds, list.Name, list.DeletedAt, [retainedItem]);

    await _repository.SaveAsync(
      replacement, TestContext.Current.CancellationToken);

    Guid[] persistedFormats = await _context.ShoppingItems
      .AsNoTracking()
      .Where(row => row.ShoppingListId == list.Id.Value)
      .Select(row => row.ProductFormatId)
      .ToArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal(new[] { retainedFormatId.Value }, persistedFormats);
  }

  [Fact(DisplayName = "Persists item added to aggregate")]
  public async Task SaveAsync_PersistsItemAddedToAggregate() {
    UserId ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedProductFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, new ShoppingListName("Weekly"));
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);
    list.AddItem(formatId, new PositiveAmount(2), true);

    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);

    var persistedItem = await _context.ShoppingItems
      .AsNoTracking()
      .Where(row => row.ShoppingListId == list.Id.Value)
      .Select(row => new {
        row.ProductFormatId,
        row.Amount,
        row.IsChecked,
      })
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal(new {
      ProductFormatId = formatId.Value,
      Amount = 2,
      IsChecked = true,
    }, persistedItem);
  }

  [Fact(DisplayName = "Persists changed item amount and checked state")]
  public async Task SaveAsync_PersistsChangedItemState() {
    UserId ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedProductFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, new ShoppingListName("Weekly"));
    list.AddItem(formatId, new PositiveAmount(1), false);
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);
    list.UpdateItem(
      Assert.Single(list.Items).Id.Value, 4, true);

    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);

    var persistedState = await _context.ShoppingItems
      .AsNoTracking()
      .Where(row => row.ShoppingListId == list.Id.Value)
      .Select(row => new { row.Amount, row.IsChecked })
      .SingleAsync(TestContext.Current.CancellationToken);
    Assert.Equal(new { Amount = 4, IsChecked = true }, persistedState);
  }

  [Fact(DisplayName = "Deletes every item when aggregate has no items")]
  public async Task SaveAsync_DeletesAllItems_WhenAggregateHasNoItems() {
    UserId ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedProductFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, new ShoppingListName("Weekly"));
    list.AddItem(formatId, new PositiveAmount(1), false);
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);
    var replacement = ShoppingList.Rehydrate(
      list.Id, list.OwnerIds, list.Name, list.DeletedAt, []);

    await _repository.SaveAsync(
      replacement, TestContext.Current.CancellationToken);

    int persistedItemCount = await _context.ShoppingItems
      .AsNoTracking()
      .CountAsync(row => row.ShoppingListId == list.Id.Value,
        TestContext.Current.CancellationToken);
    Assert.Equal(0, persistedItemCount);
  }

  [Fact(DisplayName = "Rolls back entire snapshot when child insert fails")]
  public async Task SaveAsync_RollsBackAggregate_WhenChildInsertFails() {
    UserId ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedProductFormatAsync("Milk");
    var original = ShoppingList.Create(
      ownerId, new ShoppingListName("Original"));
    original.AddItem(formatId, new PositiveAmount(2), false);
    await _repository.SaveAsync(original, TestContext.Current.CancellationToken);
    var invalidReplacement = ShoppingList.Rehydrate(
      original.Id,
      original.OwnerIds,
      new ShoppingListName("Replacement"),
      original.DeletedAt,
      [new ShoppingItem(
        new ProductFormatId(Guid.CreateVersion7()),
        new PositiveAmount(9),
        true)]);

    Exception? exception = await Record.ExceptionAsync(() =>
      _repository.SaveAsync(
        invalidReplacement, TestContext.Current.CancellationToken));

    var persisted = new {
      SaveFailed = exception is not null,
      ListName = await _context.ShoppingLists
        .AsNoTracking()
        .Where(row => row.Id == original.Id.Value)
        .Select(row => row.Name)
        .SingleAsync(TestContext.Current.CancellationToken),
      Owners = await _context.ShoppingListOwnerships
        .AsNoTracking()
        .Where(row => row.ShoppingListId == original.Id.Value)
        .Select(row => row.UserUid)
        .ToArrayAsync(TestContext.Current.CancellationToken),
      Items = await _context.ShoppingItems
        .AsNoTracking()
        .Where(row => row.ShoppingListId == original.Id.Value)
        .Select(row => new {
          row.ProductFormatId,
          row.Amount,
          row.IsChecked,
        })
        .ToArrayAsync(TestContext.Current.CancellationToken),
    };
    var expected = new {
      SaveFailed = true,
      ListName = (string?)"Original",
      Owners = new[] { ownerId.Value },
      Items = new[] {
        new {
          ProductFormatId = formatId.Value,
          Amount = 2,
          IsChecked = false,
        },
      },
    };

    Assert.Equivalent(expected, persisted, strict: true);
  }

  [Fact(DisplayName = "Saves temporary aggregate without items")]
  public async Task SaveAsync_PersistsTemporaryEmptyAggregateSnapshot() {
    UserId ownerId = await SeedUserAsync();
    var list = ShoppingList.Create(ownerId, null);

    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);

    var persisted = await _context.ShoppingLists
      .AsNoTracking()
      .Where(row => row.Id == list.Id.Value)
      .Select(row => new {
        row.Name,
        row.IsTemporary,
        OwnerCount = row.Ownerships.Count,
        ItemCount = row.Items.Count,
      })
      .SingleAsync(TestContext.Current.CancellationToken);
    var expected = new {
      Name = (string?)null,
      IsTemporary = true,
      OwnerCount = 1,
      ItemCount = 0,
    };

    Assert.Equal(expected, persisted);
  }

  [Fact(DisplayName = "Overwrites all rows owned by aggregate")]
  public async Task SaveAsync_OverwritesCompleteAggregateSnapshot() {
    UserId oldOwnerId = await SeedUserAsync();
    UserId newOwnerId = await SeedUserAsync();
    UserId sharedOwnerId = await SeedUserAsync();
    ProductFormatId removedFormatId = await SeedProductFormatAsync("Milk");
    ProductFormatId retainedFormatId = await SeedProductFormatAsync("Bread");
    var original = ShoppingList.Create(
      oldOwnerId, new ShoppingListName("Original"));
    original.AddItem(removedFormatId, new PositiveAmount(1), false);
    original.AddItem(retainedFormatId, new PositiveAmount(2), false);
    await _repository.SaveAsync(
      original, TestContext.Current.CancellationToken);
    Guid[] replacedItemIds = await _context.ShoppingItems
      .AsNoTracking()
      .Where(row => row.ShoppingListId == original.Id.Value)
      .Select(row => row.Id)
      .ToArrayAsync(TestContext.Current.CancellationToken);
    DateTime deletedAt = RemovedAt.AddDays(1);
    var replacement = ShoppingList.Rehydrate(
      original.Id,
      [newOwnerId, sharedOwnerId],
      new ShoppingListName("Replacement"),
      deletedAt,
      [new ShoppingItem(retainedFormatId, new PositiveAmount(5), true)]);

    await _repository.SaveAsync(
      replacement, TestContext.Current.CancellationToken);

    var persisted = new {
      List = await _context.ShoppingLists
        .AsNoTracking()
        .Where(row => row.Id == replacement.Id.Value)
        .Select(row => new {
          row.Name,
          row.IsTemporary,
          row.DeletedAt,
        })
        .SingleAsync(TestContext.Current.CancellationToken),
      Owners = await _context.ShoppingListOwnerships
        .AsNoTracking()
        .Where(row => row.ShoppingListId == replacement.Id.Value)
        .Select(row => row.UserUid)
        .OrderBy(id => id)
        .ToArrayAsync(TestContext.Current.CancellationToken),
      Items = await _context.ShoppingItems
        .AsNoTracking()
        .Where(row => row.ShoppingListId == replacement.Id.Value)
        .Select(row => new {
          row.Id,
          row.ProductFormatId,
          row.Amount,
          row.IsChecked,
          row.DeletedAt,
        })
        .ToArrayAsync(TestContext.Current.CancellationToken),
      ReplacedItemsRemain = await _context.ShoppingItems
        .AsNoTracking()
        .AnyAsync(row => replacedItemIds.Contains(row.Id),
          TestContext.Current.CancellationToken),
    };
    var expected = new {
      List = new {
        Name = "Replacement",
        IsTemporary = false,
        DeletedAt = (DateTime?)deletedAt,
      },
      Owners = new[] { newOwnerId.Value, sharedOwnerId.Value }.Order().ToArray(),
      Items = new[] {
        new { persisted.Items.Single().Id,
          ProductFormatId = retainedFormatId.Value,
          Amount = 5,
          IsChecked = true,
          DeletedAt = (DateTime?)null,
        },
      },
      ReplacedItemsRemain = false,
    };

    Assert.Equivalent(expected, persisted, strict: true);
  }

  [Fact(DisplayName = "Returns complete list projection with latest price")]
  public async Task GetWithPricesAsync_ReturnsCompleteProjectionWithLatestPrice() {
    UserId ownerId = await SeedUserAsync();
    var market = new SuperMarketDbEntity {
      Id = Uid.Create(),
      Name = $"Test market {Guid.CreateVersion7()}",
      LogoUrl = "https://example.test/market.png",
    };
    var brand = new ProductBrandDbEntity {
      Id = Uid.Create(),
      Name = $"Test brand {Guid.CreateVersion7()}",
    };
    UnitOfMeasureDbEntity? unit = await _context.UnitsOfMeasure
      .SingleOrDefaultAsync(value => value.Code == "kg",
        TestContext.Current.CancellationToken);
    unit ??= new UnitOfMeasureDbEntity {
      Id = Uid.Create(),
      Code = "kg",
      Name = "Kilogram",
    };
    var format = new ProductFormatDbEntity {
      Id = Uid.Create(),
      Product = new ProductDbEntity {
        Id = Uid.Create(),
        Name = "Coffee",
        SuperMarket = market,
        Brand = brand,
      },
      Quantity = 0.5m,
      UnitOfMeasure = unit,
      ImageUrl = "https://example.test/coffee.png",
      PriceSnapshots = [
        new PriceSnapshotDbEntity {
          Id = Uid.Create(),
          PriceAmount = 4.25m,
          ObservedAt = RemovedAt.AddDays(-2),
        },
        new PriceSnapshotDbEntity {
          Id = Uid.Create(),
          PriceAmount = 4.75m,
          ObservedAt = RemovedAt.AddDays(-1),
        },
      ],
    };
    _context.ProductFormats.Add(format);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var list = ShoppingList.Create(ownerId, new ShoppingListName("Weekly"));
    list.AddItem(new ProductFormatId(format.Id), new PositiveAmount(3), true);
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);
    Guid itemId = await _context.ShoppingItems
      .Where(item => item.ShoppingListId == list.Id.Value)
      .Select(item => item.Id)
      .SingleAsync(TestContext.Current.CancellationToken);

    GetShoppingList.Response? result = await _repository.GetWithPricesAsync(
      ownerId.Value, list.Id.Value, TestContext.Current.CancellationToken);

    GetShoppingList.ResponseItem item = Assert.Single(result!.Items);
    var actual = new {
      result.Id,
      result.ShoppingListName,
      Item = new {
        item.Id,
        item.ProductName,
        item.BrandName,
        MarketId = item.Market.Id,
        MarketName = item.Market.Name,
        MarketLogo = item.Market.LogoUrl?.ToString(),
        item.Amount,
        Quantity = item.Format.Quantity.Amount,
        Unit = item.Format.Quantity.UnitOfMeasure.Value,
        Price = item.Format.Price.Amount,
        Image = item.Format.ImageUrl?.ToString(),
        item.IsChecked,
      },
    };
    var expected = new {
      Id = list.Id.Value,
      ShoppingListName = (string?)"Weekly",
      Item = new {
        Id = itemId,
        ProductName = "Coffee",
        BrandName = brand.Name,
        MarketId = market.Id,
        MarketName = market.Name,
        MarketLogo = (string?)market.LogoUrl,
        Amount = 3,
        Quantity = 0.5m,
        Unit = "kg",
        Price = 4.75m,
        Image = (string?)format.ImageUrl,
        IsChecked = true,
      },
    };
    Assert.Equal(expected, actual);
  }

  [Fact(DisplayName = "Returns empty projection for owned empty list")]
  public async Task GetWithPricesAsync_ReturnsEmptyProjection_WhenListHasNoItems() {
    UserId ownerId = await SeedUserAsync();
    var list = ShoppingList.Create(ownerId, null);
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);

    GetShoppingList.Response? result = await _repository.GetWithPricesAsync(
      ownerId.Value, list.Id.Value, TestContext.Current.CancellationToken);

    var actual = new {
      result!.Id,
      result.ShoppingListName,
      ItemCount = result.Items.Count,
    };
    var expected = new {
      Id = list.Id.Value,
      ShoppingListName = (string?)null,
      ItemCount = 0,
    };
    Assert.Equal(expected, actual);
  }

  [Fact(DisplayName = "Does not return list to non-owner")]
  public async Task GetWithPricesAsync_ReturnsNull_WhenUserDoesNotOwnList() {
    UserId ownerId = await SeedUserAsync();
    UserId otherUserId = await SeedUserAsync();
    var list = ShoppingList.Create(ownerId, new ShoppingListName("Private"));
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);

    GetShoppingList.Response? result = await _repository.GetWithPricesAsync(
      otherUserId.Value, list.Id.Value, TestContext.Current.CancellationToken);

    Assert.Null(result);
  }

  [Fact(DisplayName = "Updates aggregate state")]
  public async Task UpdateAsync_PersistsRenameAddAndUpdate() {
    UserId ownerId = await SeedUserAsync();
    ProductFormatId milkId = await SeedProductFormatAsync("Milk");
    ProductFormatId breadId = await SeedProductFormatAsync("Bread");
    var list = ShoppingList.Create(ownerId, null);
    list.AddItem(milkId, new PositiveAmount(1), false);
    Guid id = await _repository.AddAsync(list, TestContext.Current.CancellationToken);
    ShoppingList persisted = Assert.IsType<ShoppingList>(await _repository.GetAsync(
      ownerId, new ShoppingListId(id), TestContext.Current.CancellationToken));

    persisted.Update("Weekly");
    persisted.UpdateItem(
      persisted.Items.Single(item => item.ProductFormatId == milkId).Id.Value,
      3, true);
    persisted.AddItem(breadId, new PositiveAmount(1), false);
    await _repository.UpdateAsync(persisted, TestContext.Current.CancellationToken);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    ShoppingList result = Assert.IsType<ShoppingList>(await _repository.GetAsync(
      ownerId,
      new ShoppingListId(id),
      TestContext.Current.CancellationToken));

    Assert.False(result.IsTemporary);
    Assert.Equal(2, result.Items.Count);
    ShoppingItem milk = result.Items.Single(item => item.ProductFormatId == milkId);
    Assert.Equal(3, milk.Amount.Value);
    Assert.True(milk.IsChecked);
  }

  [Fact(DisplayName = "Soft deletes removed item")]
  public async Task UpdateAsync_SoftDeletesRemovedItem() {
    UserId ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedProductFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, new ShoppingListName("Weekly"));
    list.AddItem(formatId, new PositiveAmount(1), false);
    Guid id = await _repository.AddAsync(list, TestContext.Current.CancellationToken);
    ShoppingList persisted = Assert.IsType<ShoppingList>(await _repository.GetAsync(
      ownerId,
      new ShoppingListId(id),
      TestContext.Current.CancellationToken));

    var replacement = ShoppingList.Rehydrate(
      persisted.Id, persisted.OwnerIds, persisted.Name, persisted.DeletedAt, []);
    await _repository.UpdateAsync(
      replacement, TestContext.Current.CancellationToken);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

    ShoppingItemDbEntity row = await _context.ShoppingItems
      .AsNoTracking()
      .SingleAsync(
        item => item.ProductFormatId == formatId.Value &&
          item.ShoppingListId == persisted.Id.Value,
        TestContext.Current.CancellationToken);
    Assert.Equal(RemovedAt, row.DeletedAt);
    ShoppingList result = Assert.IsType<ShoppingList>(await _repository.GetAsync(
      ownerId,
      new ShoppingListId(id),
      TestContext.Current.CancellationToken));
    Assert.Equal(RemovedAt, Assert.Single(result.Items).DeletedAt);
  }

  [Fact(DisplayName = "Re-adding removed format creates active replacement")]
  public async Task UpdateAsync_ReAddsPreviouslyRemovedFormat() {
    UserId ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedProductFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId.Value, "Weekly");
    list.AddItems([
      new AddItemsParams(formatId.Value, 1, false)
    ]);
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);
    var withoutItem = ShoppingList.Rehydrate(
      list.Id, list.OwnerIds, list.Name, list.DeletedAt, []);
    await _repository.UpdateAsync(
      withoutItem, TestContext.Current.CancellationToken);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var activeReplacement = ShoppingList.Rehydrate(
      list.Id,
      list.OwnerIds,
      list.Name,
      list.DeletedAt,
      [ShoppingItem.Create(formatId.Value, 4, true)]);
    await _repository.UpdateAsync(
      activeReplacement, TestContext.Current.CancellationToken);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

    ShoppingList result = Assert.IsType<ShoppingList>(await _repository.GetAsync(
      ownerId,
      list.Id,
      TestContext.Current.CancellationToken));
    ShoppingItem activeItem = Assert.Single(
      result.Items, item => item.DeletedAt is null);
    ShoppingItem deletedItem = Assert.Single(
      result.Items, item => item.DeletedAt is not null);
    Assert.Equal(4, activeItem.Amount.Value);
    Assert.True(activeItem.IsChecked);
    Assert.Equal(RemovedAt, deletedItem.DeletedAt);
    Assert.Equal(2, await _context.ShoppingItems.CountAsync(
      row => row.ProductFormatId == formatId.Value,
      TestContext.Current.CancellationToken));
  }

  [Fact(DisplayName = "Throws exact exception for invalid persisted name")]
  public async Task GetAsync_ThrowsExactException_WhenPersistedNameIsInvalid() {
    UserId ownerId = await SeedUserAsync();
    var entity = new ShoppingListDbEntity {
      Id = Uid.Create(),
      Name = "   ",
      IsTemporary = false,
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = ownerId.Value }],
    };
    _context.ShoppingLists.Add(entity);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<InvalidShoppingListNameException>(() =>
      _repository.GetByOwnerAsync(ownerId, TestContext.Current.CancellationToken));
  }

  [Fact(DisplayName = "Propagates cancelled reads")]
  public async Task GetAsync_PropagatesCancellation() {
    UserId ownerId = await SeedUserAsync();
    using var source = new CancellationTokenSource();
    await source.CancelAsync();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
      _repository.GetAsync(ownerId, new ShoppingListId(Guid.Parse("00000000-0000-7000-8000-000000000001")), source.Token));
  }

  [Fact(DisplayName = "Resets temporary list selected by ID")]
  public async Task Reset_UpdatesTemporaryList_WhenSelectedById() {
    UserId ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedProductFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, null);
    list.AddItem(formatId, new PositiveAmount(1), true);
    Guid id = await _repository.AddAsync(list, TestContext.Current.CancellationToken);

    ShoppingList persisted = Assert.IsType<ShoppingList>(await _repository.GetAsync(
      ownerId, new ShoppingListId(id), TestContext.Current.CancellationToken));
    persisted.ResetCheckedItems(ownerId.Value,
      new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc));
    await _repository.UpdateAsync(
      persisted, TestContext.Current.CancellationToken);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

    ShoppingList result = Assert.IsType<ShoppingList>(await _repository.GetAsync(
      ownerId, new ShoppingListId(id), TestContext.Current.CancellationToken));
    Assert.False(result.Items.Single().IsChecked);
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

  private async Task<ProductFormatId> SeedProductFormatAsync(string productName) {
    var market = new SuperMarketDbEntity {
      Id = Uid.Create(),
      Name = $"Test market {Guid.CreateVersion7()}",
    };
    var brand = new ProductBrandDbEntity {
      Id = Uid.Create(),
      Name = $"Test brand {Guid.CreateVersion7()}",
    };
    var unit = new UnitOfMeasureDbEntity {
      Id = Uid.Create(),
      Code = $"u{Guid.CreateVersion7():N}"[..16],
      Name = $"Test unit {Guid.CreateVersion7()}",
    };
    var product = new ProductDbEntity {
      Id = Uid.Create(),
      Name = productName,
      SuperMarket = market,
      Brand = brand,
    };
    var format = new ProductFormatDbEntity {
      Id = Uid.Create(),
      Product = product,
      Quantity = 1,
      UnitOfMeasure = unit,
      ImageUrl = "https://example.test/product.png",
    };
    _context.PriceSnapshots.Add(new PriceSnapshotDbEntity {
      Id = Uid.Create(),
      ProductFormat = format,
      PriceAmount = 1.25m,
      ObservedAt = RemovedAt.AddDays(-1),
    });
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    return new ProductFormatId(format.Id);
  }
}