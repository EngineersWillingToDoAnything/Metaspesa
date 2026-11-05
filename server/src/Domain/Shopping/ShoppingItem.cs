using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping.Errors;

namespace Metaspesa.Domain.Shopping;

public sealed class ShoppingItem {
  public ShoppingItemId Id { get; }
  public ProductFormatId ProductFormatId { get; }
  public PositiveAmount Amount { get; private set; }
  public bool IsChecked { get; private set; }
  public DateTime? DeletedAt { get; private set; }

  private ShoppingItem(
    ShoppingItemId id,
    ProductFormatId productFormatId,
    PositiveAmount amount,
    bool isChecked,
    DateTime? deletedAt
  ) {
    Id = id;
    ProductFormatId = productFormatId;
    Amount = amount;
    IsChecked = isChecked;
    DeletedAt = deletedAt;
  }

  internal static ShoppingItem Create(
    Guid productFormatId, int amount, bool isChecked
  ) {
    return new(
      new ShoppingItemId(Uid.Create()),
      new ProductFormatId(productFormatId),
      new PositiveAmount(amount),
      isChecked,
      null
    );
  }

  public static ShoppingItem Rehydrate(
    Guid id, Guid productFormatId, int amount, bool isChecked, DateTime? deletedAt
  ) {
    return new(
      new ShoppingItemId(id),
      new ProductFormatId(productFormatId),
      new PositiveAmount(amount),
      isChecked,
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