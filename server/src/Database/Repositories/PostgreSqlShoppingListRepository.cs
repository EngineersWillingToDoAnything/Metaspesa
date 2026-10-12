using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Application.Abstractions.Shopping;
using Metaspesa.Application.Shopping;
using Metaspesa.Database.Entities;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Metaspesa.Database.Repositories;

internal class PostgreSqlShoppingListRepository(
  MainContext context,
  IClock clock
) : IShoppingListRepository {
  public async Task<IReadOnlyCollection<ShoppingList>> GetByOwnerAsync(
    UserId ownerId, CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync<IReadOnlyCollection<ShoppingList>>(
    async () => {
      List<ShoppingListDbEntity> entities = await context.ShoppingLists
        .AsNoTracking()
        .Include(list => list.Ownerships)
        .Include(list => list.Items)
        .Where(list => list.Ownerships.Any(
          ownership => ownership.UserUid == ownerId.Value) &&
          list.DeletedAt == null)
        .OrderBy(list => list.Name == null)
        .ThenBy(list => list.Name)
        .ToListAsync(cancellationToken);

      return [.. entities.Select(ToDomain)];
    },
    "Couldn't get shopping lists.");

  public async Task<ShoppingList?> GetAsync(
    Guid ownerId, Guid listId, CancellationToken cancellationToken
  ) {
    ShoppingListDbEntity? entity = await context.ShoppingLists
      .AsNoTracking()
      .Include(list => list.Ownerships)
      .Include(list => list.Items)
      .Where(list => list.Id == listId && list.DeletedAt == null &&
        list.Ownerships.Any(ownership => ownership.UserUid == ownerId))
      .FirstOrDefaultAsync(cancellationToken);

    return entity is null ? null : ToDomain(entity);
  }

  public async Task<bool> ExistsAsync(
    Guid ownerId, string? name, CancellationToken cancellationToken
  ) {
    return await context.ShoppingListOwnerships.AnyAsync(
      ownership => ownership.UserUid == ownerId &&
        ownership.ShoppingList.DeletedAt == null && (
        ownership.ShoppingList.Name == null && name == null ||
        ownership.ShoppingList.Name != null &&
        name != null &&
        ownership.ShoppingList.Name == EscapeLike(name).Trim()
      ),
      cancellationToken);
  }

  public async Task<Guid> AddAsync(
    ShoppingList shoppingList, CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync(async () => {
    var entity = new ShoppingListDbEntity {
      Id = shoppingList.Id.Value,
      Name = shoppingList.Name?.Value,
      IsTemporary = shoppingList.IsTemporary,
      DeletedAt = shoppingList.DeletedAt,
      Ownerships = [.. shoppingList.OwnerIds.Select(ownerId =>
          new ShoppingListOwnershipDbEntity { UserUid = ownerId.Value })],
      Items = [.. shoppingList.Items.Select(ToEntity)],
    };
    context.ShoppingLists.Add(entity);
    await context.SaveChangesAsync(cancellationToken);
    return entity.Id;
  }, "Couldn't add shopping list.");

  public async Task UpdateAsync(
    ShoppingList shoppingList, CancellationToken cancellationToken
  ) => await PostgreSqlExceptionMapper.MapAsync(async () => {
    ShoppingListId id = shoppingList.Id;
    ShoppingListDbEntity entity = await context.ShoppingLists
      .Include(list => list.Items)
      .SingleAsync(list => list.Id == id.Value, cancellationToken);

    entity.Name = shoppingList.Name?.Value;
    entity.IsTemporary = shoppingList.IsTemporary;
    entity.DeletedAt = shoppingList.DeletedAt;

    var desiredItems = shoppingList.Items.ToDictionary(
      item => item.ProductFormatId.Value);
    foreach (ShoppingItemDbEntity existing in entity.Items.Where(
      item => item.DeletedAt == null)) {
      if (!desiredItems.Remove(existing.ProductFormatId, out ShoppingItem? desired)) {
        existing.DeletedAt = clock.GetCurrentTime();
        continue;
      }

      existing.Amount = desired.Amount.Value;
      existing.IsChecked = desired.IsChecked;
    }

    foreach (ShoppingItem addition in desiredItems.Values) {
      entity.Items.Add(ToEntity(addition));
    }
  }, "Couldn't update shopping list.");

  public async Task SaveAsync(
    ShoppingList shoppingList, CancellationToken cancellationToken
  ) {
    Guid listId = shoppingList.Id.Value;
    string? name = shoppingList.Name?.Value;
    Guid[] ownerIds = [.. shoppingList.OwnerIds.Select(owner => owner.Value)];
    ShoppingItem[] items = [.. shoppingList.Items];

    var itemIds = new Guid[items.Length];
    var productFormatIds = new Guid[items.Length];
    int[] amounts = new int[items.Length];
    bool[] checkedStates = new bool[items.Length];
    var deletedStates = new DateTime?[items.Length];

    for (int i = 0; i < items.Length; i++) {
      ShoppingItem item = items[i];

      itemIds[i] = item.Id.Value;
      productFormatIds[i] = item.ProductFormatId.Value;
      amounts[i] = item.Amount.Value;
      checkedStates[i] = item.IsChecked;
      deletedStates[i] = item.DeletedAt;
    }

    await using IDbContextTransaction transaction = await context.Database
      .BeginTransactionAsync(cancellationToken);

    await context.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO shopping.shopping_lists (
        id, name, is_temporary, deleted_at
      )
      VALUES (
        {listId}, {name}, {shoppingList.IsTemporary}, {shoppingList.DeletedAt}
      )
      ON CONFLICT (id) DO UPDATE SET
        name = EXCLUDED.name,
        is_temporary = EXCLUDED.is_temporary,
        deleted_at = EXCLUDED.deleted_at;

      DELETE FROM shopping.shopping_list_ownerships
      WHERE shopping_list_id = {listId};

      INSERT INTO shopping.shopping_list_ownerships (
        shopping_list_id, user_uid
      )
      SELECT {listId}, owner_id
      FROM unnest({ownerIds}) AS owners(owner_id);

      DELETE FROM shopping.shopping_items
      WHERE shopping_list_id = {listId};

      INSERT INTO shopping.shopping_items (
        id, shopping_list_id, product_format_id, amount, is_checked, deleted_at
      )
      SELECT
        item_id, {listId}, product_format_id, amount, is_checked, deleted_at
      FROM unnest(
        {itemIds}, {productFormatIds}, {amounts}, {checkedStates}, {deletedStates}
      ) AS items(item_id, product_format_id, amount, is_checked, deleted_at);
      """, cancellationToken);

    await transaction.CommitAsync(cancellationToken);
  }

  private static ShoppingList ToDomain(ShoppingListDbEntity entity) =>
    ShoppingList.Rehydrate(
      new ShoppingListId(entity.Id),
      entity.Ownerships.Select(ownership => new UserId(ownership.UserUid)),
      entity.Name is null ? null : new ShoppingListName(entity.Name),
      entity.DeletedAt,
      entity.Items.Select(item => ShoppingItem.Rehydrate(
        item.Id,
        item.ProductFormatId,
        item.Amount,
        item.IsChecked,
        item.DeletedAt)));

  private static ShoppingItemDbEntity ToEntity(ShoppingItem item) => new() {
    Id = item.Id.Value,
    ProductFormatId = item.ProductFormatId.Value,
    Amount = item.Amount.Value,
    IsChecked = item.IsChecked,
  };

  private static string EscapeLike(string value) => value
    .Replace("\\", "\\\\", StringComparison.Ordinal)
    .Replace("%", "\\%", StringComparison.Ordinal)
    .Replace("_", "\\_", StringComparison.Ordinal);

  public async Task<GetShoppingList.Response?> GetWithPricesAsync(
    Guid ownerId, Guid shoppingListId, CancellationToken cancellationToken
  ) {
    var projection = await context.ShoppingLists
      .AsNoTracking()
      .Where(list =>
        list.Id == shoppingListId &&
        list.DeletedAt == null &&
        list.Ownerships.Any(ownership => ownership.UserUid == ownerId))
      .Select(list => new {
        list.Id,
        list.Name,
        Items = list.Items
          .Where(item => item.DeletedAt == null)
          .Select(item => new {
            item.Id,
            item.Amount,
            item.IsChecked,
            ProductName = item.ProductFormat.Product.Name,
            BrandName = item.ProductFormat.Product.Brand.Name,
            MarketId = item.ProductFormat.Product.SuperMarket.Id,
            MarketName = item.ProductFormat.Product.SuperMarket.Name,
            MarketLogoUrl = item.ProductFormat.Product.SuperMarket.LogoUrl,
            item.ProductFormat.Quantity,
            UnitOfMeasureCode = item.ProductFormat.UnitOfMeasure.Code,
            item.ProductFormat.ImageUrl,
            Price = item.ProductFormat.PriceSnapshots
              .OrderByDescending(snapshot => snapshot.ObservedAt)
              .ThenByDescending(snapshot => snapshot.Id)
              .Select(snapshot => snapshot.PriceAmount)
              .First(),
          })
          .ToList(),
      })
      .SingleOrDefaultAsync(cancellationToken);

    if (projection is null) {
      return null;
    }

    return new GetShoppingList.Response(
      projection.Id,
      projection.Name,
      [.. projection.Items.Select(item => new GetShoppingList.ResponseItem(
        item.Id,
        item.ProductName,
        item.BrandName,
        new MarketSummary(
          item.MarketId,
          item.MarketName,
          ToUri(item.MarketLogoUrl)),
        item.Amount,
        new GetShoppingList.ResponseItemFormat(
          new Quantity(
            item.Quantity,
            new UnitOfMeasure(item.UnitOfMeasureCode)),
          new Money(item.Price),
          ToUri(item.ImageUrl)),
        item.IsChecked))]);
  }

  private static Uri? ToUri(string? value) =>
    string.IsNullOrWhiteSpace(value)
      ? null
      : new Uri(value, UriKind.Absolute);
}