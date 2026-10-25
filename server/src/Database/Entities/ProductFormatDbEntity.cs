namespace Metaspesa.Database.Entities;

internal class ProductFormatDbEntity {
  public Guid Id { get; set; }
  public Guid ProductId { get; set; }
  public decimal Quantity { get; set; }
  public string UnitOfMeasure { get; set; } = null!;
  public string ImageUrl { get; set; } = null!;

  public ProductDbEntity Product { get; set; } = null!;
  public ICollection<PriceSnapshotDbEntity> PriceSnapshots { get; set; } = [];
  public ICollection<ShoppingItemDbEntity> ShoppingItems { get; set; } = [];
}