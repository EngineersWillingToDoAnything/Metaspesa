using Metaspesa.Domain.Shopping;

namespace Metaspesa.Database.Entities;

internal class ShoppingListDbEntity {
  public Guid Id { get; set; }
  public string? Name { get; set; }
  public bool IsTemporary { get; set; }
  public DateTime? DeletedAt { get; set; }

  public ICollection<ShoppingItemDbEntity> Items { get; set; } = [];
  public ICollection<ShoppingListOwnershipDbEntity> Ownerships { get; set; } = [];
  public ICollection<PurchaseDbEntity> Purchases { get; set; } = [];

  public ShoppingList ToAggregate() {
    var primitives = new ShoppingListPrimitives(
      Id,
      Name,
      IsTemporary,
      DeletedAt,
      [.. Ownerships.Select(ownership => ownership.UserUid)],
      [.. Items.Select(item => item.ToPrimitives())]
    );

    return ShoppingList.Rehydrate(primitives);
  }
}