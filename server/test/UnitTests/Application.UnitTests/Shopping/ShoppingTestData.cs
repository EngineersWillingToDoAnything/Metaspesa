using Metaspesa.Application.Abstractions.Markets;
using Metaspesa.Domain.Identity;
using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.Application.UnitTests.Shopping;

internal static class ShoppingTestData {
  internal static readonly Guid ListId = Guid.CreateVersion7();

  public static ShoppingList List(
    Guid ownerId,
    string? name = "Weekly",
    params ShoppingItem[] items
  ) => ShoppingList.Rehydrate(
    new ShoppingListId(ListId),
    [new UserId(ownerId)],
    name is null ? null : new ShoppingListName(name),
    null,
    items);

  public static ShoppingItem Item(Guid formatId, int amount = 1, bool isChecked = false) =>
    new(new ProductFormatId(formatId), new PositiveAmount(amount), isChecked);

  public static MarketProduct MarketProduct(Guid formatId) => new(
    "Milk",
    "Brand",
    [new MarketProductFormat(
      Quantity.Create(1, "ml"),
      new Money(1.25m),
      new Uri($"https://example.test/{formatId}"),
      formatId)]);
}