using FluentAssertions;
using Numpus.Compiler;
using UnitsNet;
using Xunit;

namespace Numpus.Tests.Compiler;

public class EvaluatorTests
{
    private readonly Evaluator _evaluator = new();

    [Fact]
    public void Evaluate_SimpleNumber_ReturnsValue()
    {
        // Arrange
        var node = new NumberNode(42, null, 0, 0);

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.NumericValue.Should().Be(42);
    }

    [Fact]
    public void Evaluate_Addition_ReturnsSum()
    {
        // Arrange
        var node = new BinaryOpNode(
            new NumberNode(5, null, 0, 0),
            "+",
            new NumberNode(3, null, 0, 0),
            0, 0
        );

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.NumericValue.Should().Be(8);
    }

    [Fact]
    public void Evaluate_Subtraction_ReturnsDifference()
    {
        // Arrange
        var node = new BinaryOpNode(
            new NumberNode(10, null, 0, 0),
            "-",
            new NumberNode(3, null, 0, 0),
            0, 0
        );

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.NumericValue.Should().Be(7);
    }

    [Fact]
    public void Evaluate_Multiplication_ReturnsProduct()
    {
        // Arrange
        var node = new BinaryOpNode(
            new NumberNode(10, null, 0, 0),
            "*",
            new NumberNode(2, null, 0, 0),
            0, 0
        );

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.NumericValue.Should().Be(20);
    }

    [Fact]
    public void Evaluate_Division_ReturnsQuotient()
    {
        // Arrange
        var node = new BinaryOpNode(
            new NumberNode(20, null, 0, 0),
            "/",
            new NumberNode(4, null, 0, 0),
            0, 0
        );

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.NumericValue.Should().Be(5);
    }

    [Fact]
    public void Evaluate_DivisionByZero_ReturnsError()
    {
        // Arrange
        var node = new BinaryOpNode(
            new NumberNode(10, null, 0, 0),
            "/",
            new NumberNode(0, null, 0, 0),
            0, 0
        );

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeTrue();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Evaluate_Power_ReturnsResult()
    {
        // Arrange
        var node = new BinaryOpNode(
            new NumberNode(2, null, 0, 0),
            "^",
            new NumberNode(3, null, 0, 0),
            0, 0
        );

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.NumericValue.Should().Be(8);
    }

    [Fact]
    public void Evaluate_UnaryPlus_ReturnsPositiveValue()
    {
        // Arrange
        var node = new UnaryOpNode("+", new NumberNode(42, null, 0, 0), 0, 0);

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.NumericValue.Should().Be(42);
    }

    [Fact]
    public void Evaluate_UnaryMinus_ReturnsNegativeValue()
    {
        // Arrange
        var node = new UnaryOpNode("-", new NumberNode(42, null, 0, 0), 0, 0);

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.NumericValue.Should().Be(-42);
    }

    [Fact]
    public void Evaluate_Assignment_StoresVariable()
    {
        // Arrange
        var node = new AssignmentNode("x", new NumberNode(42, null, 0, 0), 0, 0);

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.NumericValue.Should().Be(42);
        _evaluator.GetVariables().Should().ContainKey("x");
    }

    [Fact]
    public void Evaluate_VariableReference_ReturnsStoredValue()
    {
        // Arrange
        _evaluator.Evaluate(new AssignmentNode("x", new NumberNode(42, null, 0, 0), 0, 0));
        var node = new VariableNode("x", 0, 0);

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.NumericValue.Should().Be(42);
    }

    [Fact]
    public void Evaluate_UndefinedVariable_ReturnsError()
    {
        // Arrange
        var node = new VariableNode("undefined", 0, 0);

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Undefined variable");
    }

    [Fact]
    public void Evaluate_Comment_ReturnsZero()
    {
        // Arrange
        var node = new CommentNode("This is a comment", 0, 0);

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.NumericValue.Should().Be(0);
    }

    [Fact]
    public void Evaluate_NumberWithUnit_Meters_CreatesQuantity()
    {
        // Arrange
        var node = new NumberNode(100, "m", 0, 0);

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.QuantityValue.Should().NotBeNull();
        result.QuantityValue.Should().BeOfType<Length>();
        ((Length)result.QuantityValue!).Meters.Should().Be(100);
    }

    [Fact]
    public void Evaluate_NumberWithUnit_Kilometers_CreatesQuantity()
    {
        // Arrange
        var node = new NumberNode(5, "km", 0, 0);

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.QuantityValue.Should().BeOfType<Length>();
        ((Length)result.QuantityValue!).Kilometers.Should().Be(5);
    }

    [Fact]
    public void Evaluate_AddLengths_ReturnsSum()
    {
        // Arrange
        var node = new BinaryOpNode(
            new NumberNode(100, "m", 0, 0),
            "+",
            new NumberNode(50, "m", 0, 0),
            0, 0
        );

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.QuantityValue.Should().BeOfType<Length>();
        ((Length)result.QuantityValue!).Meters.Should().Be(150);
    }

    [Fact]
    public void Evaluate_SubtractLengths_ReturnsDifference()
    {
        // Arrange
        var node = new BinaryOpNode(
            new NumberNode(100, "m", 0, 0),
            "-",
            new NumberNode(30, "m", 0, 0),
            0, 0
        );

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.QuantityValue.Should().BeOfType<Length>();
        ((Length)result.QuantityValue!).Meters.Should().Be(70);
    }

    [Fact]
    public void Evaluate_MultiplyLengths_ReturnsArea()
    {
        // Arrange
        var node = new BinaryOpNode(
            new NumberNode(5, "m", 0, 0),
            "*",
            new NumberNode(2, "m", 0, 0),
            0, 0
        );

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.QuantityValue.Should().BeOfType<Area>();
        ((Area)result.QuantityValue!).SquareMeters.Should().Be(10);
    }

    [Fact]
    public void Evaluate_DivideLengths_ReturnsRatio()
    {
        // Arrange
        var node = new BinaryOpNode(
            new NumberNode(10, "m", 0, 0),
            "/",
            new NumberNode(2, "m", 0, 0),
            0, 0
        );

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.QuantityValue.Should().BeOfType<Ratio>();
        ((Ratio)result.QuantityValue!).DecimalFractions.Should().Be(5);
    }

    [Fact]
    public void Evaluate_DivideLengthByDuration_ReturnsSpeed()
    {
        // Arrange
        var node = new BinaryOpNode(
            new NumberNode(100, "m", 0, 0),
            "/",
            new NumberNode(20, "s", 0, 0),
            0, 0
        );

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.QuantityValue.Should().BeOfType<Speed>();
        ((Speed)result.QuantityValue!).MetersPerSecond.Should().Be(5);
    }

    [Fact]
    public void Evaluate_Temperature_Celsius_CreatesQuantity()
    {
        // Arrange
        var node = new NumberNode(25, "c", 0, 0);

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.QuantityValue.Should().BeOfType<Temperature>();
        ((Temperature)result.QuantityValue!).DegreesCelsius.Should().Be(25);
    }

    [Fact]
    public void Evaluate_Mass_Kilograms_CreatesQuantity()
    {
        // Arrange
        var node = new NumberNode(75, "kg", 0, 0);

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.QuantityValue.Should().BeOfType<Mass>();
        ((Mass)result.QuantityValue!).Kilograms.Should().Be(75);
    }

    [Fact]
    public void Evaluate_Duration_Seconds_CreatesQuantity()
    {
        // Arrange
        var node = new NumberNode(60, "s", 0, 0);

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.QuantityValue.Should().BeOfType<Duration>();
        ((Duration)result.QuantityValue!).Seconds.Should().Be(60);
    }

    [Fact]
    public void Evaluate_BuiltinFunction_Sqrt_ReturnsResult()
    {
        // Arrange
        var call = new FunctionCallNode("sqrt", new AstNode[] { new NumberNode(9, null, 0, 0) }, 0, 0);

        // Act
        var result = _evaluator.Evaluate(call);

        // Assert
        result.HasError.Should().BeFalse();
        result.NumericValue.Should().Be(3);
    }

    [Fact]
    public void Evaluate_UserDefinedFunction_ReturnsComputedValue()
    {
        // Arrange
        var body = new BinaryOpNode(new VariableNode("x", 0, 0), "*", new NumberNode(2, null, 0, 0), 0, 0);
        var definition = new FunctionDefinitionNode("double", new[] { "x" }, body, 0, 0);
        var call = new FunctionCallNode("double", new AstNode[] { new NumberNode(5, null, 0, 0) }, 0, 0);

        // Act
        var definitionResult = _evaluator.Evaluate(definition);
        var callResult = _evaluator.Evaluate(call);

        // Assert
        definitionResult.HasError.Should().BeFalse();
        callResult.HasError.Should().BeFalse();
        callResult.NumericValue.Should().Be(10);
    }

    [Fact]
    public void Evaluate_UserDefinedFunction_InvalidArgumentCount_ReturnsError()
    {
        // Arrange
        var definition = new FunctionDefinitionNode("sum", new[] { "a", "b" }, new BinaryOpNode(new VariableNode("a", 0, 0), "+", new VariableNode("b", 0, 0), 0, 0), 0, 0);
        var call = new FunctionCallNode("sum", new AstNode[] { new NumberNode(5, null, 0, 0) }, 0, 0);
        _evaluator.Evaluate(definition);

        // Act
        var result = _evaluator.Evaluate(call);

        // Assert
        result.HasError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("expects 2 argument(s)");
    }

    [Fact]
    public void Evaluate_FunctionDefinitionWithDuplicateParameters_ReturnsError()
    {
        // Arrange
        var definition = new FunctionDefinitionNode("bad", new[] { "x", "x" }, new VariableNode("x", 0, 0), 0, 0);

        // Act
        var result = _evaluator.Evaluate(definition);

        // Assert
        result.HasError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("duplicate parameter names");
    }

    [Fact]
    public void Evaluate_BuiltinFunctionWithQuantityArgument_ReturnsError()
    {
        // Arrange
        var call = new FunctionCallNode("sqrt", new AstNode[] { new NumberNode(4, "m", 0, 0) }, 0, 0);

        // Act
        var result = _evaluator.Evaluate(call);

        // Assert
        result.HasError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("does not support quantity arguments");
    }

    [Fact]
    public void Evaluate_UserDefinedFunction_UsesLocalScope()
    {
        // Arrange
        _evaluator.Evaluate(new AssignmentNode("x", new NumberNode(10, null, 0, 0), 0, 0));
        var definition = new FunctionDefinitionNode(
            "shadow",
            new[] { "x" },
            new BinaryOpNode(new VariableNode("x", 0, 0), "+", new NumberNode(1, null, 0, 0), 0, 0),
            0,
            0);
        var call = new FunctionCallNode("shadow", new AstNode[] { new NumberNode(5, null, 0, 0) }, 0, 0);

        // Act
        _evaluator.Evaluate(definition);
        var callResult = _evaluator.Evaluate(call);
        var global = _evaluator.Evaluate(new VariableNode("x", 0, 0));

        // Assert
        callResult.HasError.Should().BeFalse();
        callResult.NumericValue.Should().Be(6);
        global.HasError.Should().BeFalse();
        global.NumericValue.Should().Be(10);
    }

    [Fact]
    public void Clear_RemovesAllVariables()
    {
        // Arrange
        _evaluator.Evaluate(new AssignmentNode("x", new NumberNode(42, null, 0, 0), 0, 0));
        _evaluator.Evaluate(new AssignmentNode("y", new NumberNode(10, null, 0, 0), 0, 0));

        // Act
        _evaluator.Clear();

        // Assert
        _evaluator.GetVariables().Should().BeEmpty();
    }

    [Theory]
    [InlineData(5, 3, "+", 8)]
    [InlineData(10, 3, "-", 7)]
    [InlineData(4, 5, "*", 20)]
    [InlineData(20, 4, "/", 5)]
    [InlineData(2, 8, "^", 256)]
    public void Evaluate_BasicOperations_ReturnsCorrectResult(
        double left, double right, string op, double expected)
    {
        // Arrange
        var node = new BinaryOpNode(
            new NumberNode(left, null, 0, 0),
            op,
            new NumberNode(right, null, 0, 0),
            0, 0
        );

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.NumericValue.Should().BeApproximately(expected, 0.0001);
    }

    [Fact]
    public void Evaluate_ComplexExpression_CalculatesCorrectly()
    {
        // Arrange: (5 + 3) * 2 = 16
        var node = new BinaryOpNode(
            new BinaryOpNode(
                new NumberNode(5, null, 0, 0),
                "+",
                new NumberNode(3, null, 0, 0),
                0, 0
            ),
            "*",
            new NumberNode(2, null, 0, 0),
            0, 0
        );

        // Act
        var result = _evaluator.Evaluate(node);

        // Assert
        result.HasError.Should().BeFalse();
        result.NumericValue.Should().Be(16);
    }

    [Fact]
    public void Evaluate_ChainedVariableAssignments_AllStored()
    {
        // Arrange & Act
        _evaluator.Evaluate(new AssignmentNode("x", new NumberNode(10, null, 0, 0), 0, 0));
        _evaluator.Evaluate(new AssignmentNode("y", new VariableNode("x", 0, 0), 0, 0));
        var result = _evaluator.Evaluate(new VariableNode("y", 0, 0));

        // Assert
        result.HasError.Should().BeFalse();
        result.NumericValue.Should().Be(10);
    }

    [Fact]
    public void GetDisplayValue_NumericValue_ReturnsFormattedString()
    {
        // Arrange
        var result = new CalculationResult { NumericValue = 42.5 };

        // Act
        var display = result.GetDisplayValue();

        // Assert
        display.Should().Be("42.5");
    }

    [Fact]
    public void GetDisplayValue_WithError_ReturnsErrorMessage()
    {
        // Arrange
        var result = new CalculationResult { HasError = true, ErrorMessage = "Test error" };

        // Act
        var display = result.GetDisplayValue();

        // Assert
        display.Should().Contain("Error");
        display.Should().Contain("Test error");
    }

    [Fact]
    public void GetDisplayValue_WithQuantity_ReturnsQuantityString()
    {
        // Arrange
        var result = new CalculationResult { QuantityValue = Length.FromMeters(100) };

        // Act
        var display = result.GetDisplayValue();

        // Assert
        display.Should().Contain("100");
    }
}
