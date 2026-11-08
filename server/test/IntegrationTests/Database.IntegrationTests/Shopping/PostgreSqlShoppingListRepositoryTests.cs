using Metaspesa.Application.Shopping;
using Metaspesa.Database.Entities;
using Metaspesa.Database.Repositories;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping;
using Metaspesa.Domain.Shopping.Errors;
using Microsoft.EntityFrameworkCore;

namespace Metaspesa.Database.IntegrationTests.Shopping;

[Collection("Database")]
public class PostgreSqlShoppingListRepositoryTests : IAsyncLifetime {
  private static readonly DateTime RemovedAt =
    new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);

  private readonly MainContext _context;
  private readonly PostgreSqlShoppingListRepository _repository;

  public PostgreSqlShoppingListRepositoryTests(DatabaseFixture fixture) {
    _context = fixture.CreateContext();
    _repository = new PostgreSqlShoppingListRepository(_context);
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

  [Fact(DisplayName = "Round trips shopping item identity")]
  public async Task SaveAndGetAsync_PreservesShoppingItemId() {
    Guid ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedProductFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, "Weekly");
    var item = new AddItemsParams(formatId.Value, 2, true);
    list.AddItems([item]);
    Guid originalItemId = list.ItemIdFor(item);

    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);
    ShoppingList result = Assert.IsType<ShoppingList>(
      await _repository.GetAsync(ownerId, list.Id.Value,
        TestContext.Current.CancellationToken));

    Assert.Equal(originalItemId, result.ItemIdFor(item));
  }

  [Fact(DisplayName = "Round trips shopping item deletion timestamp")]
  public async Task SaveAndGetAsync_PreservesShoppingItemDeletedAt() {
    Guid ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedProductFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, "Weekly");
    var item = new AddItemsParams(formatId.Value, 2, true);
    list.AddItems([item]);
    list.RemoveItem(list.ItemIdFor(item), RemovedAt);

    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);
    ShoppingList result = Assert.IsType<ShoppingList>(
      await _repository.GetAsync(ownerId, list.Id.Value,
        TestContext.Current.CancellationToken));

    Assert.True(result.HasRemovedItem(item, RemovedAt));
  }

  [Fact(DisplayName = "Returns only owner-accessible lists")]
  public async Task GetByOwnerAsync_IsolatesOwners() {
    Guid ownerId = await SeedUserAsync();
    Guid otherOwnerId = await SeedUserAsync();
    await _repository.SaveAsync(ShoppingList.Create(ownerId,
      "Mine"), TestContext.Current.CancellationToken);
    await _repository.SaveAsync(ShoppingList.Create(otherOwnerId,
      "Other"), TestContext.Current.CancellationToken);

    IReadOnlyCollection<GetShoppingListSummaries.Response> result = await _repository
      .GetByOwnerAsync(ownerId, TestContext.Current.CancellationToken);

    GetShoppingListSummaries.Response list = Assert.Single(result);
    Assert.Equal("Mine", list.Name);
  }

  [Fact(DisplayName = "Does not load another owner's matching list")]
  public async Task GetAsync_IsolatesOwners() {
    Guid ownerId = await SeedUserAsync();
    Guid otherOwnerId = await SeedUserAsync();
    var list = ShoppingList.Create(otherOwnerId, "Weekly");
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);

    ShoppingList? result = await _repository.GetAsync(ownerId, list.Id.Value,
      TestContext.Current.CancellationToken);

    Assert.Null(result);
  }

  [Fact(DisplayName = "Created list ID resolves only for its owner")]
  public async Task GetAsync_ByIdIsolatesOwners() {
    Guid ownerId = await SeedUserAsync();
    Guid otherOwnerId = await SeedUserAsync();
    var list = ShoppingList.Create(ownerId, "Rest list");
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);

    ShoppingList? result = await _repository.GetAsync(
      otherOwnerId, list.Id.Value, TestContext.Current.CancellationToken);

    Assert.Null(result);
  }

  [Fact(DisplayName = "Orders named lists before temporary list")]
  public async Task GetByOwnerAsync_OrdersNamedListsBeforeTemporaryList() {
    Guid ownerId = await SeedUserAsync();
    await _repository.SaveAsync(ShoppingList.Create(ownerId, null),
      TestContext.Current.CancellationToken);
    await _repository.SaveAsync(ShoppingList.Create(ownerId,
      "Weekly"), TestContext.Current.CancellationToken);

    IReadOnlyCollection<GetShoppingListSummaries.Response> result = await _repository.GetByOwnerAsync(
      ownerId, TestContext.Current.CancellationToken);

    Assert.Collection(
      result,
      list => Assert.Equal("Weekly", list.Name),
      list => Assert.True(list.IsTemporary));
  }

  [Fact(DisplayName = "Doesn't find named list case-insensitively")]
  public async Task ExistsAsync_DoesNotMatchNamedList_WithDifferentCase() {
    Guid ownerId = await SeedUserAsync();
    await _repository.SaveAsync(ShoppingList.Create(ownerId,
      "Weekly"), TestContext.Current.CancellationToken);

    bool exists = await _repository.ExistsAsync(
      ownerId,
      "WEEKLY",
      TestContext.Current.CancellationToken);

    Assert.False(exists);
  }

  [Fact(DisplayName = "Finds named list trimming name")]
  public async Task ExistsAsync_MatchesNamedList_TrimmingName() {
    Guid ownerId = await SeedUserAsync();
    await _repository.SaveAsync(ShoppingList.Create(ownerId,
      "Weekly"), TestContext.Current.CancellationToken);

    bool exists = await _repository.ExistsAsync(
      ownerId,
      "   Weekly   ",
      TestContext.Current.CancellationToken);

    Assert.True(exists);
  }

  [Fact(DisplayName = "Does not find another owner's matching list")]
  public async Task ExistsAsync_IsolatesOwners() {
    Guid ownerId = await SeedUserAsync();
    Guid otherOwnerId = await SeedUserAsync();
    await _repository.SaveAsync(ShoppingList.Create(otherOwnerId, null),
      TestContext.Current.CancellationToken);

    bool exists = await _repository.ExistsAsync(
      ownerId, null, TestContext.Current.CancellationToken);

    Assert.False(exists);
  }

  [Fact(DisplayName = "Saves complete aggregate snapshot")]
  public async Task SaveAsync_PersistsCompleteAggregateSnapshot() {
    Guid ownerId = await SeedUserAsync();
    ProductFormatId milkId = await SeedProductFormatAsync("Milk");
    ProductFormatId breadId = await SeedProductFormatAsync("Bread");
    var list = ShoppingList.Create(ownerId, "Weekly");
    list.AddItems([new AddItemsParams(milkId.Value, 2, true)]);
    list.AddItems([new AddItemsParams(breadId.Value, 1, false)]);

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
      Owners = new[] { ownerId },
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
    Guid ownerId = await SeedUserAsync();
    var list = ShoppingList.Create(ownerId, "Original");
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
    Guid retainedOwnerId = await SeedUserAsync();
    Guid removedOwnerId = await SeedUserAsync();
    var list = ShoppingList.Create(retainedOwnerId, "Shared");
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);
    _context.ShoppingListOwnerships.Add(new ShoppingListOwnershipDbEntity {
      ShoppingListId = list.Id.Value,
      UserUid = removedOwnerId,
    });
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);

    Guid[] persistedOwners = await _context.ShoppingListOwnerships
      .AsNoTracking()
      .Where(row => row.ShoppingListId == list.Id.Value)
      .Select(row => row.UserUid)
      .ToArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal(new[] { retainedOwnerId }, persistedOwners);
  }

  [Fact(DisplayName = "Inserts ownership added to aggregate")]
  public async Task SaveAsync_InsertsOwnershipAddedToAggregate() {
    Guid originalOwnerId = await SeedUserAsync();
    Guid addedOwnerId = await SeedUserAsync();
    var original = ShoppingList.Create(
      originalOwnerId, "Shared");
    await _repository.SaveAsync(original, TestContext.Current.CancellationToken);
    _context.ShoppingListOwnerships.Add(new ShoppingListOwnershipDbEntity {
      ShoppingListId = original.Id.Value,
      UserUid = addedOwnerId,
    });
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    ShoppingList replacement = Assert.IsType<ShoppingList>(await _repository.GetAsync(
      originalOwnerId, original.Id.Value, TestContext.Current.CancellationToken));
    await _context.ShoppingListOwnerships
      .Where(row => row.ShoppingListId == original.Id.Value && row.UserUid == addedOwnerId)
      .ExecuteDeleteAsync(TestContext.Current.CancellationToken);

    await _repository.SaveAsync(replacement, TestContext.Current.CancellationToken);

    Guid[] persistedOwners = await _context.ShoppingListOwnerships
      .AsNoTracking()
      .Where(row => row.ShoppingListId == original.Id.Value)
      .Select(row => row.UserUid)
      .OrderBy(id => id)
      .ToArrayAsync(TestContext.Current.CancellationToken);
    Guid[] expectedOwners =
      [.. new[] { originalOwnerId, addedOwnerId }.Order()];
    Assert.Equal(expectedOwners, persistedOwners);
  }

  [Fact(DisplayName = "Physically removes item absent from aggregate")]
  public async Task SaveAsync_RemovesItemAbsentFromAggregate() {
    Guid ownerId = await SeedUserAsync();
    ProductFormatId retainedFormatId = await SeedProductFormatAsync("Bread");
    ProductFormatId removedFormatId = await SeedProductFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, "Weekly");
    list.AddItems([new AddItemsParams(retainedFormatId.Value, 1, false)]);
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);
    _context.ShoppingItems.Add(new ShoppingItemDbEntity {
      Id = Guid.CreateVersion7(),
      ShoppingListId = list.Id.Value,
      ProductFormatId = removedFormatId.Value,
      Amount = 1,
    });
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

    await _repository.SaveAsync(
      list, TestContext.Current.CancellationToken);

    Guid[] persistedFormats = await _context.ShoppingItems
      .AsNoTracking()
      .Where(row => row.ShoppingListId == list.Id.Value)
      .Select(row => row.ProductFormatId)
      .ToArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal(new[] { retainedFormatId.Value }, persistedFormats);
  }

  [Fact(DisplayName = "Persists item added to aggregate")]
  public async Task SaveAsync_PersistsItemAddedToAggregate() {
    Guid ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedProductFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, "Weekly");
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);
    list.AddItems([new AddItemsParams(formatId.Value, 2, true)]);

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
    Guid ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedProductFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, "Weekly");
    list.AddItems([new AddItemsParams(formatId.Value, 1, false)]);
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);
    list.UpdateItem(
      list.ItemIdFor(new AddItemsParams(formatId.Value, 1, false)), 4, true);

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
    Guid ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedProductFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, "Weekly");
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);
    _context.ShoppingItems.Add(new ShoppingItemDbEntity {
      Id = Guid.CreateVersion7(),
      ShoppingListId = list.Id.Value,
      ProductFormatId = formatId.Value,
      Amount = 1,
    });
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

    await _repository.SaveAsync(
      list, TestContext.Current.CancellationToken);

    int persistedItemCount = await _context.ShoppingItems
      .AsNoTracking()
      .CountAsync(row => row.ShoppingListId == list.Id.Value,
        TestContext.Current.CancellationToken);
    Assert.Equal(0, persistedItemCount);
  }

  [Fact(DisplayName = "Rolls back entire snapshot when child insert fails")]
  public async Task SaveAsync_RollsBackAggregate_WhenChildInsertFails() {
    Guid ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedProductFormatAsync("Milk");
    var original = ShoppingList.Create(
      ownerId, "Original");
    original.AddItems([new AddItemsParams(formatId.Value, 2, false)]);
    await _repository.SaveAsync(original, TestContext.Current.CancellationToken);
    original.Update("Replacement");
    original.AddItems([new AddItemsParams(Guid.CreateVersion7(), 9, true)]);

    Exception? exception = await Record.ExceptionAsync(() =>
      _repository.SaveAsync(
        original, TestContext.Current.CancellationToken));

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
      Owners = new[] { ownerId },
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
    Guid ownerId = await SeedUserAsync();
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
    Guid oldOwnerId = await SeedUserAsync();
    Guid newOwnerId = await SeedUserAsync();
    Guid sharedOwnerId = await SeedUserAsync();
    ProductFormatId removedFormatId = await SeedProductFormatAsync("Milk");
    ProductFormatId retainedFormatId = await SeedProductFormatAsync("Bread");
    var list = ShoppingList.Create(newOwnerId, "Replacement");
    list.AddItems([new AddItemsParams(retainedFormatId.Value, 5, true)]);
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);

    // Sharing has no public domain operation yet; load the persisted ownerships through the repository.
    _context.ShoppingListOwnerships.Add(new ShoppingListOwnershipDbEntity {
      ShoppingListId = list.Id.Value,
      UserUid = sharedOwnerId,
    });
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    ShoppingList replacement = Assert.IsType<ShoppingList>(await _repository.GetAsync(
      newOwnerId, list.Id.Value, TestContext.Current.CancellationToken));

    // Seed stale rows so saving the aggregate must replace the complete stored snapshot.
    DateTime deletedAt = RemovedAt.AddDays(1);
    await _context.ShoppingLists.Where(row => row.Id == list.Id.Value)
      .ExecuteUpdateAsync(setters => setters
        .SetProperty(row => row.Name, "Original")
        .SetProperty(row => row.DeletedAt, deletedAt), TestContext.Current.CancellationToken);
    await _context.ShoppingListOwnerships.Where(row => row.ShoppingListId == list.Id.Value)
      .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
    await _context.ShoppingItems.Where(row => row.ShoppingListId == list.Id.Value)
      .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
    _context.ShoppingListOwnerships.Add(new ShoppingListOwnershipDbEntity {
      ShoppingListId = list.Id.Value,
      UserUid = oldOwnerId,
    });
    Guid[] replacedItemIds = [Guid.CreateVersion7(), Guid.CreateVersion7()];
    _context.ShoppingItems.AddRange(
      new ShoppingItemDbEntity {
        Id = replacedItemIds[0], ShoppingListId = list.Id.Value,
        ProductFormatId = removedFormatId.Value, Amount = 1,
      },
      new ShoppingItemDbEntity {
        Id = replacedItemIds[1], ShoppingListId = list.Id.Value,
        ProductFormatId = retainedFormatId.Value, Amount = 2,
      });
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        DeletedAt = (DateTime?)null,
      },
      Owners = new[] { newOwnerId, sharedOwnerId }.Order().ToArray(),
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
    Guid ownerId = await SeedUserAsync();
    var market = new MarketDbEntity {
      Id = Uid.Create(),
      Name = $"Test market {Guid.CreateVersion7()}",
      LogoUrl = "https://example.test/market.png",
    };
    var brand = new BrandDbEntity {
      Id = Uid.Create(),
      Name = $"Test brand {Guid.CreateVersion7()}",
    };
    var format = new ProductFormatDbEntity {
      Id = Uid.Create(),
      Product = new ProductDbEntity {
        Id = Uid.Create(),
        Name = "Coffee",
        SuperMarket = market,
        Brand = brand,
      },
      Quantity = 500m,
      UnitOfMeasure = "g",
      ImageUrl = "https://example.test/coffee.png",
      PriceSnapshots = [
        new PriceSnapshotDbEntity {
          Id = Uid.Create(),
          PriceAmount = 4.25m,
          ObservedOn = DateOnly.FromDateTime(RemovedAt.AddDays(-2)),
        },
        new PriceSnapshotDbEntity {
          Id = Uid.Create(),
          PriceAmount = 4.75m,
          ObservedOn = DateOnly.FromDateTime(RemovedAt.AddDays(-1)),
        },
      ],
    };
    _context.ProductFormats.Add(format);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    var list = ShoppingList.Create(ownerId, "Weekly");
    list.AddItems([new AddItemsParams(format.Id, 3, true)]);
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);
    Guid itemId = await _context.ShoppingItems
      .Where(item => item.ShoppingListId == list.Id.Value)
      .Select(item => item.Id)
      .SingleAsync(TestContext.Current.CancellationToken);

    GetShoppingList.Response? result = await _repository.GetWithPricesAsync(
      ownerId, list.Id.Value, TestContext.Current.CancellationToken);

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
        Quantity = 500m,
        Unit = "g",
        Price = 4.75m,
        Image = (string?)format.ImageUrl,
        IsChecked = true,
      },
    };
    Assert.Equal(expected, actual);
  }

  [Fact(DisplayName = "Returns empty projection for owned empty list")]
  public async Task GetWithPricesAsync_ReturnsEmptyProjection_WhenListHasNoItems() {
    Guid ownerId = await SeedUserAsync();
    var list = ShoppingList.Create(ownerId, null);
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);

    GetShoppingList.Response? result = await _repository.GetWithPricesAsync(
      ownerId, list.Id.Value, TestContext.Current.CancellationToken);

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
    Guid ownerId = await SeedUserAsync();
    Guid otherUserId = await SeedUserAsync();
    var list = ShoppingList.Create(ownerId, "Private");
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);

    GetShoppingList.Response? result = await _repository.GetWithPricesAsync(
      otherUserId, list.Id.Value, TestContext.Current.CancellationToken);

    Assert.Null(result);
  }

  [Fact(DisplayName = "Projects stored summary without hydrating the aggregate")]
  public async Task GetByOwnerAsync_ProjectsStoredNameWithoutHydratingAggregate() {
    // Arrange
    Guid ownerId = await SeedUserAsync();
    var entity = new ShoppingListDbEntity {
      Id = Uid.Create(),
      Name = "   ",
      IsTemporary = false,
      Ownerships = [new ShoppingListOwnershipDbEntity { UserUid = ownerId }],
    };
    _context.ShoppingLists.Add(entity);
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

    // Act
    IReadOnlyCollection<GetShoppingListSummaries.Response> result = await _repository.GetByOwnerAsync(
      ownerId, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(new GetShoppingListSummaries.Response(entity.Name, entity.Id, entity.IsTemporary),
      Assert.Single(result));
  }

  [Fact(DisplayName = "Propagates cancelled reads")]
  public async Task GetAsync_PropagatesCancellation() {
    Guid ownerId = await SeedUserAsync();
    using var source = new CancellationTokenSource();
    await source.CancelAsync();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
      _repository.GetAsync(ownerId, Guid.Parse("00000000-0000-7000-8000-000000000001"), source.Token));
  }

  [Fact(DisplayName = "Resets temporary list selected by ID")]
  public async Task SaveAsync_PersistsCheckoutResetForTemporaryList() {
    Guid ownerId = await SeedUserAsync();
    ProductFormatId formatId = await SeedProductFormatAsync("Milk");
    var list = ShoppingList.Create(ownerId, null);
    var item = new AddItemsParams(formatId.Value, 1, true);
    list.AddItems([item]);
    await _repository.SaveAsync(list, TestContext.Current.CancellationToken);

    ShoppingList persisted = Assert.IsType<ShoppingList>(await _repository.GetAsync(ownerId, list.Id.Value, TestContext.Current.CancellationToken));
    persisted.Checkout(ownerId,
      new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc));
    await _repository.SaveAsync(
      persisted, TestContext.Current.CancellationToken);

    ShoppingList result = Assert.IsType<ShoppingList>(await _repository.GetAsync(ownerId, list.Id.Value, TestContext.Current.CancellationToken));
    Assert.True(result.IncludesItem(item with { IsChecked = false }));
  }

  private async Task<Guid> SeedUserAsync() {
    var id = Guid.CreateVersion7();
    _context.Users.Add(new UserDbEntity {
      Uid = id,
      Username = id.ToString(),
      EncryptedPassword = "x",
      RoleId = UserRoleIds.Shopper,
    });
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    return id;
  }

  private async Task<ProductFormatId> SeedProductFormatAsync(string productName) {
    var market = new MarketDbEntity {
      Id = Uid.Create(),
      Name = $"Test market {Guid.CreateVersion7()}",
    };
    var brand = new BrandDbEntity {
      Id = Uid.Create(),
      Name = $"Test brand {Guid.CreateVersion7()}",
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
      UnitOfMeasure = "g",
      ImageUrl = "https://example.test/product.png",
    };
    _context.PriceSnapshots.Add(new PriceSnapshotDbEntity {
      Id = Uid.Create(),
      ProductFormat = format,
      PriceAmount = 1.25m,
      ObservedOn = DateOnly.FromDateTime(RemovedAt.AddDays(-1)),
    });
    await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    return new ProductFormatId(format.Id);
  }
}