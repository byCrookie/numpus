using System.Globalization;
using UnitsNet;

namespace Numpus.Compiler;

public class CalculationResult
{
    public bool HasError { get; init; }

    public string? ErrorMessage { get; init; }

    public double NumericValue { get; init; }

    public IQuantity? QuantityValue { get; init; }

    public bool IsQuantity => QuantityValue is not null;

    public string GetDisplayValue()
    {
        if (HasError)
        {
            return ErrorMessage is null ? "Error" : $"Error: {ErrorMessage}";
        }

        if (QuantityValue is { } quantity)
        {
            var quantityText = quantity.ToString();
            return string.IsNullOrWhiteSpace(quantityText) ? "Unknown quantity" : quantityText;
        }

        return NumericValue.ToString(CultureInfo.InvariantCulture);
    }

    public CalculationResult Clone() => new()
    {
        HasError = HasError,
        ErrorMessage = ErrorMessage,
        NumericValue = NumericValue,
        QuantityValue = QuantityValue
    };

    public static CalculationResult FromNumeric(double value) => new() { NumericValue = value };

    public static CalculationResult FromQuantity(IQuantity quantity) => new() { QuantityValue = quantity };

    public static CalculationResult FromError(string message) => new()
    {
        HasError = true,
        ErrorMessage = string.IsNullOrWhiteSpace(message) ? "Unknown error" : message
    };
}
