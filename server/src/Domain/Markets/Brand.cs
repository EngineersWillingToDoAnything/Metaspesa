using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Markets;

public sealed class Brand {
  public BrandId Id { get; }
  public BrandName Name { get; }

  private Brand(BrandId id, BrandName name) {
    Id = id;
    Name = name;
  }

  public static Brand Create(string rawName) {
    BrandId id = new(Uid.Create());
    BrandName name = new(rawName);

    return new Brand(id, name);
  }

  public static Brand Rehydrate(BrandId id, BrandName name) {
    return new Brand(id, name);
  }
}