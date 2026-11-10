using Metaspesa.Domain.Markets;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping.Errors;
using Metaspesa.Domain.Shopping.Events;

namespace Metaspesa.Domain.Shopping;

public sealed class ShoppingItem {
  private bool _isChecked;
  private PositiveAmount _amount;
  private readonly ProductFormatId _productFormatId;
  private DateTime? _deletedAt;

  public ShoppingItemId Id { get; }

  private ShoppingItem(
    ShoppingItemId id,
    ProductFormatId productFormatId,
    PositiveAmount amount,
    bool isChecked,
    DateTime? deletedAt
  ) {
    Id = id;
    _productFormatId = productFormatId;
    _amount = amount;
    _isChecked = isChecked;
    _deletedAt = deletedAt;
  }

  internal static ShoppingItem Create(
    AddItemsParams parameters
  ) {
    return new ShoppingItem(
      new ShoppingItemId(Uid.Create()),
      new ProductFormatId(parameters.ProductFormatUid),
      new PositiveAmount(parameters.Amount),
      parameters.IsChecked,
      null);
  }

  internal static ShoppingItem Rehydrate(ShoppingItemPrimitives primitives) {
    return new ShoppingItem(
      new ShoppingItemId(primitives.Id),
      new ProductFormatId(primitives.ProductFormatId),
      new PositiveAmount(primitives.Amount),
      primitives.IsChecked,
      primitives.DeletedAt);
  }

  public ShoppingItemPrimitives ToPrimitives() {
    return new ShoppingItemPrimitives(
      Id.Value, _productFormatId.Value, _amount.Value, _isChecked, _deletedAt);
  }

  internal void Delete(DateTime deletedAt) {
    if (_deletedAt.HasValue) {
      throw new ShoppingItemAlreadyDeletedException(Id);
    }

    _deletedAt = deletedAt;
  }

  internal void Update(int? amount, bool? isChecked) {
    if (!amount.HasValue && !isChecked.HasValue) {
      throw new EmptyShoppingItemUpdateException();
    }

    if (amount.HasValue) {
      _amount = new PositiveAmount(amount.Value);
    }
    if (isChecked.HasValue) {
      _isChecked = isChecked.Value;
    }
  }

  internal bool CanBeCheckedOut() => !WasDeleted() && _isChecked;

  internal bool WasDeleted() => _deletedAt is not null;

  internal bool WasDeletedAt(DateTime deletedAt) => _deletedAt == deletedAt;

  internal bool MatchesItem(AddItemsParams item) {
    return ReferencesSameFormat(item) &&
      _amount.Value == item.Amount &&
      _isChecked == item.IsChecked;
  }

  internal CheckedOutItem CreateCheckedOutItem() {
    return new CheckedOutItem(_productFormatId.Value, _amount.Value);
  }

  internal bool ReferencesSameFormat(AddItemsParams item) {
    return _productFormatId.Value == item.ProductFormatUid;
  }
}

public sealed record ShoppingItemPrimitives(
  Guid Id, Guid ProductFormatId, int Amount, bool IsChecked, DateTime? DeletedAt
);