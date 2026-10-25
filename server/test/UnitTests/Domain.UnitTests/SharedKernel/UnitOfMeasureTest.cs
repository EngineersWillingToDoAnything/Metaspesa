using Metaspesa.Domain.SharedKernel;
using Metaspesa.Domain.SharedKernel.Errors;

namespace Metaspesa.Domain.UnitTests.SharedKernel;

public static class UnitOfMeasureTest {
  public class Create {
    [Theory(DisplayName = "Creates unit of measure when value is supported")]
    [InlineData("unit")]
    [InlineData("g")]
    [InlineData("wash")]
    [InlineData("ml")]
    [InlineData("dose")]
    [InlineData("kg")]
    [InlineData("mg")]
    [InlineData("l")]
    [InlineData("cl")]
    public void UnitOfMeasure_Created_WhenValueIsSupported(string value) {
      // Act
      var unitOfMeasure = UnitOfMeasure.Create(value);

      // Assert
      Assert.NotEqual(default, unitOfMeasure);
    }

    [Fact(DisplayName = "Normalizes value to trimmed value")]
    public void UnitOfMeasure_Created_TrimsValue() {
      // Act
      var unitOfMeasure = UnitOfMeasure.Create(" g ");

      // Assert
      Assert.Equal("g", unitOfMeasure.Value);
    }

    [Fact(DisplayName = "Normalizes value to lowercase")]
    public void UnitOfMeasure_Created_LowercasesValue() {
      // Act
      var unitOfMeasure = UnitOfMeasure.Create("G");

      // Assert
      Assert.Equal("g", unitOfMeasure.Value);
    }

    [Theory(DisplayName = "Throws specific exception when value is invalid")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("box")]
    [InlineData("uds")]
    [InlineData("kilogramos")]
    public void UnitOfMeasure_ThrowsException_WhenValueIsInvalid(string value) {
      // Act
      void action() => UnitOfMeasure.Create(value);

      // Assert
      Assert.Throws<InvalidUnitOfMeasureException>(action);
    }
  }

  public class ToCanon {
    [Theory]
    [InlineData("kg", "g")]
    [InlineData("mg", "g")]
    [InlineData("g", "g")]
    [InlineData("l", "ml")]
    [InlineData("cl", "ml")]
    [InlineData("ml", "ml")]
    [InlineData("unit", "unit")]
    [InlineData("wash", "wash")]
    [InlineData("dose", "dose")]
    public void ToCanonical_ReturnsCanonicalUnit(string unit, string expected) {
      var expectedUnit = UnitOfMeasure.Create(expected);

      UnitOfMeasure canonicalUnit = UnitOfMeasure.Create(unit).ToCanon();

      Assert.Equal(expectedUnit, canonicalUnit);
    }

    [Fact]
    public void ToCanon_PreservesOriginalUnit() {
      var original = UnitOfMeasure.Create("kg");

      _ = original.ToCanon();

      Assert.Equal("kg", original.Value);
    }
  }

  public class ConvertToCanonValue {
    [Theory]
    [InlineData("kg", 1000)]
    [InlineData("mg", 0.001)]
    [InlineData("g", 1)]
    [InlineData("l", 1000)]
    [InlineData("cl", 10)]
    [InlineData("ml", 1)]
    [InlineData("unit", 1)]
    [InlineData("wash", 1)]
    [InlineData("dose", 1)]
    public void ConvertToCanonValue_ReturnsCorrectValue(
      string unit, decimal expected
    ) {
      var unitOfMeasure = UnitOfMeasure.Create(unit);

      decimal canonValue = unitOfMeasure.ConvertToCanonValue(1);

      Assert.Equal(expected, canonValue);
    }

    [Fact]
    public void ConvertToCanonValue_ThrowsException_WhenAmountIsNegative() {
      var unitOfMeasure = UnitOfMeasure.Create("g");

      void action() => unitOfMeasure.ConvertToCanonValue(-5);

      Assert.Throws<InvalidQuantityAmountException>(action);
    }
  }

  public class Equality {
    [Fact(DisplayName = "Two units representing the same unit are equal")]
    public void UnitOfMeasure_Equal_WhenSameNormalizedValue() {
      // Arrange
      var unit1 = UnitOfMeasure.Create("g");
      var unit2 = UnitOfMeasure.Create(" G ");

      // Act & Assert
      Assert.Equal(unit1, unit2);
    }
  }
}