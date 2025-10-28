using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using UnitsNet;

namespace Numpus.Compiler;

public class Evaluator
{
    private static readonly IReadOnlyDictionary<string, (string Quantity, string Unit)> _unitAliases =
        new Dictionary<string, (string Quantity, string Unit)>(StringComparer.OrdinalIgnoreCase)
        {
            ["m"] = ("Length", "Meter"),
            ["cm"] = ("Length", "Centimeter"),
            ["mm"] = ("Length", "Millimeter"),
            ["km"] = ("Length", "Kilometer"),
            ["in"] = ("Length", "Inch"),
            ["ft"] = ("Length", "Foot"),
            ["c"] = ("Temperature", "DegreeCelsius"),
            ["f"] = ("Temperature", "DegreeFahrenheit"),
            ["k"] = ("Temperature", "Kelvin"),
            ["degc"] = ("Temperature", "DegreeCelsius"),
            ["degf"] = ("Temperature", "DegreeFahrenheit"),
            ["degk"] = ("Temperature", "Kelvin"),
            ["kg"] = ("Mass", "Kilogram"),
            ["g"] = ("Mass", "Gram"),
            ["lb"] = ("Mass", "Pound"),
            ["s"] = ("Duration", "Second"),
            ["sec"] = ("Duration", "Second"),
            ["ms"] = ("Duration", "Millisecond"),
            ["min"] = ("Duration", "Minute"),
            ["mins"] = ("Duration", "Minute"),
            ["h"] = ("Duration", "Hour"),
            ["hr"] = ("Duration", "Hour")
        };

    private static readonly UnitParser _unitParser = UnitsNetSetup.Default.UnitParser;
    private static readonly UnitAbbreviationsCache _unitAbbreviations = UnitsNetSetup.Default.UnitAbbreviations;
    private static readonly IReadOnlyList<QuantityInfo> _quantities = Quantity.Infos;

    private readonly Dictionary<string, CalculationResult> _globalScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<Dictionary<string, CalculationResult>> _variableScopes = new();
    private readonly Dictionary<string, FunctionDefinition> _functions = new(StringComparer.OrdinalIgnoreCase);

    public Evaluator()
    {
        _variableScopes.Push(_globalScope);
    }

    public CalculationResult Evaluate(AstNode node) => node switch
    {
        NumberNode number => EvaluateNumber(number),
        VariableNode variable => EvaluateVariable(variable),
        UnaryOpNode unary => EvaluateUnary(unary),
        BinaryOpNode binary => EvaluateBinary(binary),
        AssignmentNode assignment => EvaluateAssignment(assignment),
        FunctionDefinitionNode functionDefinition => EvaluateFunctionDefinition(functionDefinition),
        FunctionCallNode functionCall => EvaluateFunctionCall(functionCall),
        CommentNode => CalculationResult.FromNumeric(0),
        _ => CalculationResult.FromError($"Unsupported node type '{node.GetType().Name}'.")
    };

    public IReadOnlyDictionary<string, CalculationResult> GetVariables() =>
        new ReadOnlyDictionary<string, CalculationResult>(_globalScope);

    public void Clear()
    {
        _globalScope.Clear();
        _functions.Clear();
        _variableScopes.Clear();
        _variableScopes.Push(_globalScope);
    }

    private Dictionary<string, CalculationResult> CurrentScope => _variableScopes.Peek();

    private CalculationResult EvaluateNumber(NumberNode node)
    {
        if (string.IsNullOrWhiteSpace(node.Unit))
        {
            return CalculationResult.FromNumeric(node.Value);
        }

        if (TryCreateQuantity(node.Value, node.Unit!, out var quantity, out var error))
        {
            return CalculationResult.FromQuantity(quantity);
        }

        return CalculationResult.FromError(error ?? $"Unrecognized unit '{node.Unit}'.");
    }

    private CalculationResult EvaluateVariable(VariableNode node)
    {
        foreach (var scope in _variableScopes)
        {
            if (scope.TryGetValue(node.Name, out var value))
            {
                return value.Clone();
            }
        }

        return CalculationResult.FromError($"Undefined variable '{node.Name}'.");
    }

    private CalculationResult EvaluateUnary(UnaryOpNode node)
    {
        var operand = Evaluate(node.Operand);
        if (operand.HasError)
        {
            return operand;
        }

        return node.Operator switch
        {
            "+" => operand.Clone(),
            "-" when operand.IsQuantity => ScaleQuantity(operand.QuantityValue!, -1),
            "-" => CalculationResult.FromNumeric(-operand.NumericValue),
            _ => CalculationResult.FromError($"Unsupported unary operator '{node.Operator}'.")
        };
    }

    private CalculationResult EvaluateBinary(BinaryOpNode node)
    {
        var left = Evaluate(node.Left);
        if (left.HasError)
        {
            return left;
        }

        var right = Evaluate(node.Right);
        if (right.HasError)
        {
            return right;
        }

        return node.Operator switch
        {
            "+" => Add(left, right),
            "-" => Subtract(left, right),
            "*" => Multiply(left, right),
            "/" => Divide(left, right),
            "^" => Power(left, right),
            _ => CalculationResult.FromError($"Unsupported operator '{node.Operator}'.")
        };
    }

    private CalculationResult EvaluateAssignment(AssignmentNode node)
    {
        var value = Evaluate(node.Value);
        if (value.HasError)
        {
            return value;
        }

        CurrentScope[node.Name] = value.Clone();
        return value;
    }

    private CalculationResult EvaluateFunctionDefinition(FunctionDefinitionNode node)
    {
        if (node.Parameters.Count != node.Parameters.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            return CalculationResult.FromError($"Function '{node.Name}' has duplicate parameter names.");
        }

        _functions[node.Name] = new FunctionDefinition(node.Name, node.Parameters, node.Body);
        return CalculationResult.FromNumeric(0);
    }

    private CalculationResult EvaluateFunctionCall(FunctionCallNode node)
    {
        var evaluatedArguments = new List<CalculationResult>(node.Arguments.Count);
        foreach (var argument in node.Arguments)
        {
            var argumentValue = Evaluate(argument);
            if (argumentValue.HasError)
            {
                return argumentValue;
            }

            evaluatedArguments.Add(argumentValue);
        }

        if (TryEvaluateBuiltinFunction(node.Name, evaluatedArguments, out var builtinResult))
        {
            return builtinResult;
        }

        if (!_functions.TryGetValue(node.Name, out var function))
        {
            return CalculationResult.FromError($"Undefined function '{node.Name}'.");
        }

        if (function.Parameters.Count != evaluatedArguments.Count)
        {
            return CalculationResult.FromError($"Function '{node.Name}' expects {function.Parameters.Count} argument(s) but received {evaluatedArguments.Count}.");
        }

        var localScope = new Dictionary<string, CalculationResult>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < function.Parameters.Count; index++)
        {
            localScope[function.Parameters[index]] = evaluatedArguments[index].Clone();
        }

        _variableScopes.Push(localScope);
        try
        {
            var result = Evaluate(function.Body);
            return result.Clone();
        }
        finally
        {
            _variableScopes.Pop();
        }
    }

    private bool TryEvaluateBuiltinFunction(string name, IReadOnlyList<CalculationResult> arguments, out CalculationResult result)
    {
        switch (name.ToLowerInvariant())
        {
            case "sin":
                result = EvaluateUnaryScalar("sin", arguments, Math.Sin);
                return true;
            case "cos":
                result = EvaluateUnaryScalar("cos", arguments, Math.Cos);
                return true;
            case "tan":
                result = EvaluateUnaryScalar("tan", arguments, Math.Tan);
                return true;
            case "asin":
                result = EvaluateUnaryScalar("asin", arguments, Math.Asin, value => value is < -1 or > 1
                    ? "Function 'asin' requires an argument in the range [-1, 1]."
                    : null);
                return true;
            case "acos":
                result = EvaluateUnaryScalar("acos", arguments, Math.Acos, value => value is < -1 or > 1
                    ? "Function 'acos' requires an argument in the range [-1, 1]."
                    : null);
                return true;
            case "atan":
                result = EvaluateUnaryScalar("atan", arguments, Math.Atan);
                return true;
            case "sqrt":
                result = EvaluateUnaryScalar("sqrt", arguments, Math.Sqrt, value => value < 0
                    ? "Function 'sqrt' requires a non-negative argument."
                    : null);
                return true;
            case "abs":
                result = EvaluateUnaryScalar("abs", arguments, Math.Abs);
                return true;
            case "exp":
                result = EvaluateUnaryScalar("exp", arguments, Math.Exp);
                return true;
            case "ln":
                result = EvaluateUnaryScalar("ln", arguments, Math.Log, value => value <= 0
                    ? "Function 'ln' requires a positive argument."
                    : null);
                return true;
            case "log":
                result = EvaluateLog(arguments);
                return true;
            case "pow":
                result = EvaluateBinaryScalar("pow", arguments, Math.Pow);
                return true;
            case "min":
                result = EvaluateMultiArgScalar("min", arguments, Math.Min);
                return true;
            case "max":
                result = EvaluateMultiArgScalar("max", arguments, Math.Max);
                return true;
            case "round":
                result = EvaluateUnaryScalar("round", arguments, Math.Round);
                return true;
            case "floor":
                result = EvaluateUnaryScalar("floor", arguments, Math.Floor);
                return true;
            case "ceil":
            case "ceiling":
                result = EvaluateUnaryScalar("ceiling", arguments, Math.Ceiling);
                return true;
            default:
                result = default!;
                return false;
        }
    }

    private CalculationResult EvaluateUnaryScalar(
        string functionName,
        IReadOnlyList<CalculationResult> arguments,
        Func<double, double> operation,
        Func<double, string?>? validator = null)
    {
        if (arguments.Count != 1)
        {
            return CalculationResult.FromError(BuildArgumentCountMessage(functionName, 1, arguments.Count));
        }

        if (!TryGetScalar(arguments[0], functionName, 0, out var value, out var errorMessage))
        {
            return CalculationResult.FromError(errorMessage!);
        }

        if (validator is not null)
        {
            var validationError = validator(value);
            if (validationError is not null)
            {
                return CalculationResult.FromError(validationError);
            }
        }

        var result = operation(value);
        if (double.IsNaN(result) || double.IsInfinity(result))
        {
            return CalculationResult.FromError($"Function '{functionName}' produced an invalid result.");
        }

        return CalculationResult.FromNumeric(result);
    }

    private CalculationResult EvaluateBinaryScalar(
        string functionName,
        IReadOnlyList<CalculationResult> arguments,
        Func<double, double, double> operation,
        Func<double, double, string?>? validator = null)
    {
        if (arguments.Count != 2)
        {
            return CalculationResult.FromError(BuildArgumentCountMessage(functionName, 2, arguments.Count));
        }

        if (!TryGetScalar(arguments[0], functionName, 0, out var left, out var leftError))
        {
            return CalculationResult.FromError(leftError!);
        }

        if (!TryGetScalar(arguments[1], functionName, 1, out var right, out var rightError))
        {
            return CalculationResult.FromError(rightError!);
        }

        if (validator is not null)
        {
            var validationError = validator(left, right);
            if (validationError is not null)
            {
                return CalculationResult.FromError(validationError);
            }
        }

        var result = operation(left, right);
        if (double.IsNaN(result) || double.IsInfinity(result))
        {
            return CalculationResult.FromError($"Function '{functionName}' produced an invalid result.");
        }

        return CalculationResult.FromNumeric(result);
    }

    private CalculationResult EvaluateMultiArgScalar(
        string functionName,
        IReadOnlyList<CalculationResult> arguments,
        Func<double, double, double> reducer)
    {
        if (arguments.Count == 0)
        {
            return CalculationResult.FromError($"Function '{functionName}' expects at least one argument.");
        }

        var hasValue = false;
        var accumulator = 0d;
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!TryGetScalar(arguments[index], functionName, index, out var value, out var errorMessage))
            {
                return CalculationResult.FromError(errorMessage!);
            }

            if (!hasValue)
            {
                accumulator = value;
                hasValue = true;
                continue;
            }

            accumulator = reducer(accumulator, value);
        }

        if (double.IsNaN(accumulator) || double.IsInfinity(accumulator))
        {
            return CalculationResult.FromError($"Function '{functionName}' produced an invalid result.");
        }

        return CalculationResult.FromNumeric(accumulator);
    }

    private CalculationResult EvaluateLog(IReadOnlyList<CalculationResult> arguments)
    {
        const string functionName = "log";

        if (arguments.Count == 1)
        {
            if (!TryGetScalar(arguments[0], functionName, 0, out var value, out var errorMessage))
            {
                return CalculationResult.FromError(errorMessage!);
            }

            if (value <= 0)
            {
                return CalculationResult.FromError("Function 'log' requires a positive argument.");
            }

            var result = Math.Log10(value);
            if (double.IsNaN(result) || double.IsInfinity(result))
            {
                return CalculationResult.FromError("Function 'log' produced an invalid result.");
            }

            return CalculationResult.FromNumeric(result);
        }

        if (arguments.Count == 2)
        {
            if (!TryGetScalar(arguments[0], functionName, 0, out var value, out var valueError))
            {
                return CalculationResult.FromError(valueError!);
            }

            if (!TryGetScalar(arguments[1], functionName, 1, out var baseValue, out var baseError))
            {
                return CalculationResult.FromError(baseError!);
            }

            if (value <= 0)
            {
                return CalculationResult.FromError("Function 'log' requires a positive argument.");
            }

            if (baseValue <= 0 || Math.Abs(baseValue - 1d) < double.Epsilon)
            {
                return CalculationResult.FromError("Function 'log' requires a positive base not equal to 1.");
            }

            var result = Math.Log(value, baseValue);
            if (double.IsNaN(result) || double.IsInfinity(result))
            {
                return CalculationResult.FromError("Function 'log' produced an invalid result.");
            }

            return CalculationResult.FromNumeric(result);
        }

        return CalculationResult.FromError(BuildArgumentCountMessage(functionName, "one or two", arguments.Count));
    }

    private static bool TryGetScalar(
        CalculationResult argument,
        string functionName,
        int argumentIndex,
        out double scalar,
        out string? errorMessage)
    {
        if (argument.IsQuantity)
        {
            errorMessage = $"Function '{functionName}' does not support quantity arguments.";
            scalar = default;
            return false;
        }

        if (argument.HasError)
        {
            errorMessage = argument.ErrorMessage ?? $"Argument {argumentIndex + 1} for function '{functionName}' contains an error.";
            scalar = default;
            return false;
        }

        scalar = argument.NumericValue;
        errorMessage = null;
        return true;
    }

    private static string BuildArgumentCountMessage(string functionName, object expected, int actual)
    {
        return $"Function '{functionName}' expects {expected} argument(s) but received {actual}.";
    }

    private sealed record FunctionDefinition(string Name, IReadOnlyList<string> Parameters, AstNode Body);

    private CalculationResult Add(CalculationResult left, CalculationResult right)
    {
        if (!left.IsQuantity && !right.IsQuantity)
        {
            return CalculationResult.FromNumeric(left.NumericValue + right.NumericValue);
        }

        if (left.IsQuantity && right.IsQuantity)
        {
            return CombineQuantities(left.QuantityValue!, right.QuantityValue!, (l, r) => l + r);
        }

        return CalculationResult.FromError("Cannot add quantities and scalars together.");
    }

    private CalculationResult Subtract(CalculationResult left, CalculationResult right)
    {
        if (!left.IsQuantity && !right.IsQuantity)
        {
            return CalculationResult.FromNumeric(left.NumericValue - right.NumericValue);
        }

        if (left.IsQuantity && right.IsQuantity)
        {
            return CombineQuantities(left.QuantityValue!, right.QuantityValue!, (l, r) => l - r);
        }

        return CalculationResult.FromError("Cannot subtract a quantity from a scalar or vice versa.");
    }

    private CalculationResult Multiply(CalculationResult left, CalculationResult right)
    {
        if (!left.IsQuantity && !right.IsQuantity)
        {
            return CalculationResult.FromNumeric(left.NumericValue * right.NumericValue);
        }

        if (left.IsQuantity && right.IsQuantity)
        {
            return OperateQuantities(left.QuantityValue!, right.QuantityValue!, (l, r) => l * r, "*");
        }

        if (left.IsQuantity)
        {
            return ScaleQuantity(left.QuantityValue!, right.NumericValue);
        }

        if (right.IsQuantity)
        {
            return ScaleQuantity(right.QuantityValue!, left.NumericValue);
        }

        return CalculationResult.FromError("Cannot multiply a scalar by a quantity.");
    }

    private CalculationResult Divide(CalculationResult left, CalculationResult right)
    {
        if (!left.IsQuantity && !right.IsQuantity)
        {
            if (Math.Abs(right.NumericValue) < double.Epsilon)
            {
                return CalculationResult.FromError("Division by zero.");
            }

            return CalculationResult.FromNumeric(left.NumericValue / right.NumericValue);
        }

        if (left.IsQuantity && right.IsQuantity)
        {
            var leftQuantity = left.QuantityValue!;
            var rightQuantity = right.QuantityValue!;
            var treatScalarAsRatio = leftQuantity.QuantityInfo.UnitType == rightQuantity.QuantityInfo.UnitType;
            return OperateQuantities(leftQuantity, rightQuantity, (l, r) => l / r, "/", treatScalarAsRatio);
        }

        if (left.IsQuantity)
        {
            if (Math.Abs(right.NumericValue) < double.Epsilon)
            {
                return CalculationResult.FromError("Division by zero.");
            }

            return ScaleQuantity(left.QuantityValue!, 1 / right.NumericValue);
        }

        if (right.IsQuantity)
        {
            return CalculationResult.FromError("Cannot divide a scalar by a quantity.");
        }

        if (Math.Abs(right.NumericValue) < double.Epsilon)
        {
            return CalculationResult.FromError("Division by zero.");
        }

        return CalculationResult.FromNumeric(left.NumericValue / right.NumericValue);
    }

    private CalculationResult OperateQuantities(IQuantity left, IQuantity right, Func<dynamic, dynamic, dynamic> operation, string symbol, bool treatScalarAsRatio = false)
    {
        try
        {
            dynamic dynamicLeft = left;
            dynamic dynamicRight = right;
            var operationResult = operation(dynamicLeft, dynamicRight);
            if (treatScalarAsRatio && operationResult is double scalarResult)
            {
                return CalculationResult.FromQuantity(Ratio.FromDecimalFractions(scalarResult));
            }

            return ConvertDynamicResult(operationResult);
        }
        catch (Exception)
        {
            return CalculationResult.FromError($"Cannot apply {symbol} to {left.QuantityInfo.Name} and {right.QuantityInfo.Name}.");
        }
    }

    private CalculationResult ConvertDynamicResult(object? result)
    {
        if (result is null)
        {
            return CalculationResult.FromError("Operation produced no result.");
        }

        if (result is CalculationResult calculationResult)
        {
            return calculationResult.Clone();
        }

        if (result is IQuantity quantity)
        {
            return CalculationResult.FromQuantity(quantity);
        }

        if (result is double doubleValue)
        {
            return CalculationResult.FromNumeric(doubleValue);
        }

        if (result is QuantityValue quantityValue)
        {
            return CalculationResult.FromNumeric((double)quantityValue);
        }

        if (result is IConvertible convertible)
        {
            return CalculationResult.FromNumeric(convertible.ToDouble(CultureInfo.InvariantCulture));
        }

        return CalculationResult.FromError($"Unsupported result type '{result.GetType().Name}'.");
    }

    private CalculationResult Power(CalculationResult left, CalculationResult right)
    {
        if (left.IsQuantity || right.IsQuantity)
        {
            return CalculationResult.FromError("Exponentiation is only supported for scalars.");
        }

        return CalculationResult.FromNumeric(Math.Pow(left.NumericValue, right.NumericValue));
    }

    private bool TryCreateQuantity(double value, string unit, out IQuantity quantity, out string? error)
    {
        if (TryResolveAlias(value, unit, out quantity, out error))
        {
            return true;
        }

        if (TryFromAbbreviation(value, unit, out quantity, out error))
        {
            return true;
        }

        if (TryFromKnownUnitDefinitions(value, unit, out quantity))
        {
            error = null;
            return true;
        }

        if (TryFromUnitParser(value, unit, out quantity))
        {
            error = null;
            return true;
        }

        error ??= BuildUnknownUnitMessage(unit);
        quantity = default!;
        return false;
    }

    private static bool TryResolveAlias(double value, string unit, out IQuantity quantity, out string? error)
    {
        quantity = default!;
        error = null;

        var trimmed = unit.Trim();
        if (!_unitAliases.TryGetValue(trimmed, out var mapping))
        {
            return false;
        }

        if (Quantity.TryFrom(value, mapping.Quantity, mapping.Unit, out var resolvedQuantity) && resolvedQuantity is not null)
        {
            quantity = resolvedQuantity;
            return true;
        }

        error = $"Unable to resolve unit alias '{unit}'.";
        return false;
    }

    private static bool TryFromAbbreviation(double value, string unit, out IQuantity quantity, out string? error)
    {
        quantity = default!;
        error = null;

        var trimmed = unit.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        try
        {
            quantity = Quantity.FromUnitAbbreviation(value, trimmed);
            return true;
        }
        catch (UnitsNetException ex) when (ex is UnitNotFoundException)
        {
            return false;
        }
        catch (UnitsNetException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryFromKnownUnitDefinitions(double value, string unit, out IQuantity quantity)
    {
        var trimmed = unit.Trim();
        const StringComparison comparison = StringComparison.OrdinalIgnoreCase;

        foreach (var quantityInfo in _quantities)
        {
            foreach (var unitInfo in quantityInfo.UnitInfos)
            {
                if (MatchesUnitName(unitInfo, trimmed, comparison))
                {
                    quantity = Quantity.From(value, unitInfo.Value);
                    return true;
                }
            }
        }

        quantity = default!;
        return false;
    }

    private static bool TryFromUnitParser(double value, string unit, out IQuantity quantity)
    {
        foreach (var quantityInfo in _quantities)
        {
            if (_unitParser.TryParse(unit, quantityInfo.UnitType, CultureInfo.InvariantCulture, out var parsedUnit))
            {
                quantity = Quantity.From(value, parsedUnit);
                return true;
            }
        }

        quantity = default!;
        return false;
    }

    private static bool MatchesUnitName(UnitInfo unitInfo, string candidate, StringComparison comparison)
    {
        if (string.Equals(unitInfo.Name, candidate, comparison) || string.Equals(unitInfo.PluralName, candidate, comparison))
        {
            return true;
        }

        return GetAbbreviationsSafe(unitInfo)
            .Any(abbreviation => string.Equals(abbreviation, candidate, comparison));
    }

    private static IEnumerable<string> GetAbbreviationsSafe(UnitInfo unitInfo)
    {
        try
        {
            var unitType = unitInfo.Value.GetType();
            var unitValue = Convert.ToInt32(unitInfo.Value, CultureInfo.InvariantCulture);
            return _unitAbbreviations.GetUnitAbbreviations(unitType, unitValue, CultureInfo.InvariantCulture);
        }
        catch (UnitsNetException)
        {
            return Array.Empty<string>();
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<string>();
        }
    }

    private static string BuildUnknownUnitMessage(string unit)
    {
        var suggestions = GetUnitSuggestions(unit);
        return suggestions.Count switch
        {
            0 => $"Unrecognized unit '{unit}'.",
            1 => $"Unrecognized unit '{unit}'. Did you mean '{suggestions[0]}'?",
            _ => $"Unrecognized unit '{unit}'. Did you mean one of: {string.Join(", ", suggestions)}?"
        };
    }

    private static List<string> GetUnitSuggestions(string unit)
    {
        var normalized = NormalizeUnit(unit);
        if (string.IsNullOrEmpty(normalized))
        {
            return new List<string>();
        }

        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var quantityInfo in _quantities)
        {
            foreach (var unitInfo in quantityInfo.UnitInfos)
            {
                foreach (var representation in EnumerateUnitRepresentations(unitInfo))
                {
                    var normalizedRepresentation = NormalizeUnit(representation);
                    if (normalizedRepresentation.StartsWith(normalized, StringComparison.OrdinalIgnoreCase) ||
                        normalized.StartsWith(normalizedRepresentation, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(representation))
                        {
                            results.Add(representation);
                        }
                    }
                }
            }
        }

        return results.Take(5).ToList();
    }

    private static IEnumerable<string> EnumerateUnitRepresentations(UnitInfo unitInfo)
    {
        yield return unitInfo.Name;
        yield return unitInfo.PluralName;

        foreach (var abbreviation in GetAbbreviationsSafe(unitInfo))
        {
            yield return abbreviation;
        }
    }

    private static string NormalizeUnit(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var characters = trimmed.Where(ch => !char.IsWhiteSpace(ch)).ToArray();
        return new string(characters).ToLowerInvariant();
    }

    private CalculationResult CombineQuantities(IQuantity left, IQuantity right, Func<double, double, double> operation)
    {
        if (left.QuantityInfo.UnitType != right.QuantityInfo.UnitType)
        {
            return CalculationResult.FromError("Cannot combine quantities of different types.");
        }

        var targetUnit = left.Unit;
        var rightConverted = right.ToUnit(targetUnit);
        var leftValue = (double)left.Value;
        var rightValue = (double)rightConverted.Value;
        var resultValue = operation(leftValue, rightValue);

        try
        {
            var resultQuantity = Quantity.From(resultValue, targetUnit);
            return CalculationResult.FromQuantity(resultQuantity);
        }
        catch (Exception ex)
        {
            return CalculationResult.FromError($"Failed to combine quantities: {ex.Message}");
        }
    }

    private CalculationResult ScaleQuantity(IQuantity quantity, double factor)
    {
        try
        {
            var scaledValue = (double)quantity.Value * factor;
            var resultQuantity = Quantity.From(scaledValue, quantity.Unit);
            return CalculationResult.FromQuantity(resultQuantity);
        }
        catch (Exception ex)
        {
            return CalculationResult.FromError($"Failed to scale quantity: {ex.Message}");
        }
    }
}
