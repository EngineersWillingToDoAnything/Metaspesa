namespace Metaspesa.Database.Entities;

internal class BrandDbEntity {
  public Guid Id { get; set; }
  public string Name { get; set; } = null!;

  public ICollection<ProductDbEntity> Products { get; set; } = [];
}