using Metaspesa.Application.Markets;

namespace Metaspesa.Application.UnitTests.Markets;

public class UnitOfMeasureNormalizerTests {
  [Theory(DisplayName = "Resolves aliases for gram")]
  [InlineData("gr")]
  [InlineData("gramo")]
  [InlineData("gramos")]
  public void Normalizer_ResolvesAliasesForGram(string input) {
    string? actual = UnitOfMeasureNormalizer.Normalize(input);

    Assert.Equal("g", actual);
  }

  [Theory(DisplayName = "Resolves aliases for kilogram")]
  [InlineData("kilo")]
  [InlineData("kilos")]
  [InlineData("kilogramo")]
  public void Normalizer_ResolvesAliasesForKilogram(string input) {
    string? actual = UnitOfMeasureNormalizer.Normalize(input);

    Assert.Equal("kg", actual);
  }

  [Theory(DisplayName = "Resolves aliases for liter")]
  [InlineData("litro")]
  [InlineData("litros")]
  public void Normalizer_ResolvesAliasesForLiter(string input) {
    string? actual = UnitOfMeasureNormalizer.Normalize(input);

    Assert.Equal("l", actual);
  }

  [Theory(DisplayName = "Resolves aliases for unit")]
  [InlineData("ud")]
  [InlineData("ud.")]
  [InlineData("uds")]
  [InlineData("uds.")]
  [InlineData("unidad")]
  [InlineData("unidades")]
  public void Normalizer_ResolvesAliasesForUnit(string input) {
    string? actual = UnitOfMeasureNormalizer.Normalize(input);

    Assert.Equal("unit", actual);
  }

  [Theory(DisplayName = "Resolves aliases for wash")]
  [InlineData("lav")]
  [InlineData("lavados")]
  public void Normalizer_ResolvesAliasesForWash(string input) {
    string? actual = UnitOfMeasureNormalizer.Normalize(input);

    Assert.Equal("wash", actual);
  }

  [Theory(DisplayName = "Resolves aliases for dose")]
  [InlineData("ds")]
  public void Normalizer_ResolvesAliasesForDose(string input) {
    string? actual = UnitOfMeasureNormalizer.Normalize(input);

    Assert.Equal("dose", actual);
  }

  [Fact(DisplayName = "Resolves with case insensitivity")]
  public void Normalize_ResolvesWithCaseInsensitivity() {
    string? normalizedUnit = UnitOfMeasureNormalizer.Normalize("GrAmOs");

    Assert.Equal("g", normalizedUnit);
  }

  [Fact(DisplayName = "Resolves with leading and trailing whitespace")]
  public void Normalize_ResolvesWithLeadingAndTrailingWhitespace() {
    string? normalizedUnit = UnitOfMeasureNormalizer.Normalize("  kilos  ");

    Assert.Equal("kg", normalizedUnit);
  }

  [Theory(DisplayName = "Preserves unrecognized units for domain validation")]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("box")]
  public void Normalize_PreservesUnrecognizedUnits_ForDomainValidation(
    string? unit
  ) {
    string? normalizedUnit = UnitOfMeasureNormalizer.Normalize(unit);

    Assert.Equal(unit, normalizedUnit);
  }
}