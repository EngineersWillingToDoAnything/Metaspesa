using Metaspesa.Domain.Markets;
using Metaspesa.Domain.Markets.Errors;
using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.Shopping;

namespace Metaspesa.Domain.UnitTests.Shopping;

public class ShoppingItemTests {
  [Fact(DisplayName = "Creates shopping item from typed values")]
  public void Constructor_ExposesTypedValues() {
    var formatId = new ProductFormatId(Guid.Parse("00000000-0000-7000-8000-000000000003"));
    var amount = new PositiveAmount(2);

    var item = new ShoppingItem(formatId, amount, true);

    Assert.Equal(formatId, item.ProductFormatId);
    Assert.Equal(amount, item.Amount);
    Assert.True(item.IsChecked);
  }

  [Fact(DisplayName = "Creates item from primitive values")]
  public void Create_MapsPrimitiveValues() {
    var formatId = Guid.Parse("00000000-0000-7000-8000-000000000003");

    var item = ShoppingItem.Create(formatId, 2, true);

    Assert.Equal(new {
      ProductFormatId = formatId,
      Amount = 2,
      IsChecked = true,
      DeletedAt = (DateTime?)null,
    }, new {
      ProductFormatId = item.ProductFormatId.Value,
      Amount = item.Amount.Value,
      item.IsChecked,
      item.DeletedAt,
    });
  }

  [Fact(DisplayName = "Creates item with version 7 ID")]
  public void Create_GeneratesVersion7Id() {
    var item = ShoppingItem.Create(
      Guid.Parse("00000000-0000-7000-8000-000000000003"), 1, false);

    Assert.Equal(7, item.Id.Value.Version);
  }

  [Fact(DisplayName = "Create rejects invalid product format ID")]
  public void Create_ThrowsExactException_WhenProductFormatIdIsInvalid() {
    Assert.Throws<InvalidProductFormatIdException>(() =>
      ShoppingItem.Create(Guid.Empty, 1, false));
  }

  [Fact(DisplayName = "Rehydrates exact persisted state")]
  public void Rehydrate_MapsPersistedState() {
    var itemId = Guid.Parse("00000000-0000-7000-8000-000000000004");
    var formatId = Guid.Parse("00000000-0000-7000-8000-000000000003");
    var date = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    var item = ShoppingItem.Rehydrate(itemId, formatId, 3, true, date);

    Assert.Equal(new {
      Id = itemId,
      ProductFormatId = formatId,
      Amount = 3,
      IsChecked = true,
      DeletedAt = (DateTime?)date,
    }, new {
      Id = item.Id.Value,
      ProductFormatId = item.ProductFormatId.Value,
      Amount = item.Amount.Value,
      item.IsChecked,
      item.DeletedAt,
    });
  }

  [Fact(DisplayName = "Cannot be checked out if deleted")]
  public void CanBeCheckedOut_ReturnsFalse_WhenDeleted() {
    var item = ShoppingItem.Create(Guid.CreateVersion7(), 1, true);
    item.Delete(DateTime.UtcNow);

    bool canBeCheckedOut = item.CanBeCheckedOut();

    Assert.False(canBeCheckedOut);
  }

  [Fact(DisplayName = "Cannot be checked out if not checked")]
  public void CanBeCheckedOut_ReturnsFalse_WhenNotChecked() {
    var item = ShoppingItem.Create(Guid.CreateVersion7(), 1, false);

    bool canBeCheckedOut = item.CanBeCheckedOut();

    Assert.False(canBeCheckedOut);
  }

  [Fact(DisplayName = "Can be checked out if checked and not deleted")]
  public void CanBeCheckedOut_ReturnsTrue_WhenCheckedAndNotDeleted() {
    var item = ShoppingItem.Create(Guid.CreateVersion7(), 1, true);

    bool canBeCheckedOut = item.CanBeCheckedOut();

    Assert.True(canBeCheckedOut);
  }
}