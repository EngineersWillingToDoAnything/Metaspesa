namespace Metaspesa.Database.Entities;

internal class MarketDbEntity {
  public Guid Id { get; set; }
  public string Name { get; set; } = null!;
  public string? LogoUrl { get; set; }

  public ICollection<ProductDbEntity> Products { get; set; } = [];
}