using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.Shopping;

public sealed class ShoppingItem(
  ProductFormatId productFormatId,
  PositiveAmount amount,
  bool isChecked,
  ShoppingItemId? id = null,
  DateTime? deletedAt = null
) {

  public ShoppingItemId Id { get; } = id ?? new ShoppingItemId(Uid.Create());
  public ProductFormatId ProductFormatId { get; } = productFormatId;
  public PositiveAmount Amount { get; private set; } = amount;
  public bool IsChecked { get; private set; } = isChecked;
  public DateTime? DeletedAt { get; private set; } = deletedAt;

  public static ShoppingItem Create(
    Guid productFormatId, int amount, bool isChecked
  ) {
    return new(
      new ProductFormatId(productFormatId),
      new PositiveAmount(amount),
      isChecked,
      new ShoppingItemId(Uid.Create()),
      null
    );
  }

  public static ShoppingItem Rehydrate(
    Guid id, Guid productFormatId, int amount, bool isChecked, DateTime? deletedAt
  ) {
    return new(
      new ProductFormatId(productFormatId),
      new PositiveAmount(amount),
      isChecked,
      new ShoppingItemId(id),
      deletedAt
    );
  }

  internal void Delete(DateTime deletedAt) {
    if (DeletedAt.HasValue) {
      throw new ShoppingItemAlreadyDeletedException(Id);
    }

    DeletedAt = deletedAt;
  }

  internal void Update(int? amount, bool? isChecked) {
    if (!amount.HasValue && !isChecked.HasValue) {
      throw new EmptyShoppingItemUpdateException();
    }

    if (amount.HasValue) {
      Amount = new PositiveAmount(amount.Value);
    }
    if (isChecked.HasValue) {
      IsChecked = isChecked.Value;
    }
  }

  public bool CanBeCheckedOut() => DeletedAt is null && IsChecked;
}