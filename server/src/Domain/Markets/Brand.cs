using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.Domain.Markets;

public sealed class Brand : AggregateRoot {
  private readonly BrandName _name;

  public BrandId Id { get; }

  private Brand(BrandId id, BrandName name) {
    Id = id;
    _name = name;
  }

  public static Brand Create(string rawName) {
    BrandId id = new(Uid.Create());
    BrandName name = new(rawName);

    return new Brand(id, name);
  }

  public static Brand Rehydrate(BrandPrimitives primitives) {
    ArgumentNullException.ThrowIfNull(primitives);

    return new Brand(new(primitives.Id), new(primitives.Name));
  }

  public BrandPrimitives ToPrimitives() {
    return new BrandPrimitives(Id.Value, _name.Value);
  }
}

public sealed record BrandPrimitives(Guid Id, string Name);