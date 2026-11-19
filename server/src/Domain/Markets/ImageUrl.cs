using Metaspesa.Domain.Markets.Errors;

namespace Metaspesa.Domain.Markets;

public readonly record struct ImageUrl {
  public Uri Value { get; }

  public ImageUrl(Uri value) {
    Value = value;
  }

  public static ImageUrl Create(Uri? value) {
    if (value is null || !value.IsAbsoluteUri) {
      throw new InvalidImageUrlException(value);
    }

    return new ImageUrl(value);
  }

  public override string ToString() => Value.ToString();
}