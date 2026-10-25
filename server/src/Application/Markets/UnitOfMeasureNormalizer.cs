namespace Metaspesa.Application.Markets;

public static class UnitOfMeasureNormalizer {
  public static string? Normalize(string? unit) {
    return unit?.Trim().ToUpperInvariant() switch {
      "GR" or "GRAMO" or "GRAMOS" => "g",
      "KILO" or "KILOS" or "KILOGRAMO" or "KILOGRAMOS" => "kg",
      "LITRO" or "LITROS" => "l",
      "UD" or "UD." or "UDS" or "UDS." or "UNIDAD" or "UNIDADES" => "unit",
      "LAV" or "LAVADOS" => "wash",
      "DS" => "dose",
      _ => unit,
    };
  }
}