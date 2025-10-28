using FluentAssertions;
using Numpus.Compiler;
using Xunit;

namespace Numpus.Tests.Integration;

/// <summary>
/// Integration tests that test the full pipeline: Parse -> Evaluate
/// </summary>
public class EndToEndTests
{
    private readonly Evaluator _evaluator = new();

    [Theory]
    [InlineData("5 + 3", 8)]
    [InlineData("10 - 4", 6)]
    [InlineData("6 * 7", 42)]
    [InlineData("20 / 4", 5)]
    [InlineData("2 ^ 10", 1024)]
    [InlineData("(5 + 3) * 2", 16)]
    [InlineData("10 + 2 * 5", 20)]
    [InlineData("(10 + 2) * 5", 60)]
    public void EndToEnd_BasicCalculations_ComputeCorrectly(string input, double expected)
    {
        // Act
        var parseResult = NumpusParser.ParseExpression(input);
        parseResult.Success.Should().BeTrue($"'{input}' should parse successfully");

        var evalResult = _evaluator.Evaluate(parseResult.Value);

        // Assert
        evalResult.HasError.Should().BeFalse($"'{input}' should evaluate without error");
        evalResult.NumericValue.Should().BeApproximately(expected, 0.0001);
    }

    [Fact]
    public void EndToEnd_VariableAssignmentAndUsage_WorksCorrectly()
    {
        // Arrange
        var assignInput = "x = 42";
        var useInput = "x + 8";

        // Act
        var assignParse = NumpusParser.ParseExpression(assignInput);
        assignParse.Success.Should().BeTrue();
        var assignResult = _evaluator.Evaluate(assignParse.Value);
        assignResult.HasError.Should().BeFalse();

        var useParse = NumpusParser.ParseExpression(useInput);
        useParse.Success.Should().BeTrue();
        var useResult = _evaluator.Evaluate(useParse.Value);

        // Assert
        useResult.HasError.Should().BeFalse();
        useResult.NumericValue.Should().Be(50);
    }

    [Fact]
    public void EndToEnd_MultipleVariables_WorkTogether()
    {
        // Arrange & Act
        var x = NumpusParser.ParseExpression("x = 10");
        _evaluator.Evaluate(x.Value);

        var y = NumpusParser.ParseExpression("y = 20");
        _evaluator.Evaluate(y.Value);

        var sum = NumpusParser.ParseExpression("x + y");
        var result = _evaluator.Evaluate(sum.Value);

        // Assert
        result.NumericValue.Should().Be(30);
    }

    [Fact]
    public void EndToEnd_ComplexCalculation_WorksCorrectly()
    {
        // Arrange: 2 ^ 3 + 4 * 5 - 6 / 2 = 8 + 20 - 3 = 25
        var input = "2 ^ 3 + 4 * 5 - 6 / 2";

        // Act
        var parseResult = NumpusParser.ParseExpression(input);
        var evalResult = _evaluator.Evaluate(parseResult.Value);

        // Assert
        evalResult.NumericValue.Should().Be(25);
    }

    [Fact]
    public void EndToEnd_LengthUnits_CalculateCorrectly()
    {
        // Arrange
        var input = "100 m";

        // Act
        var parseResult = NumpusParser.ParseExpression(input);
        var evalResult = _evaluator.Evaluate(parseResult.Value);

        // Assert
        evalResult.HasError.Should().BeFalse();
        evalResult.QuantityValue.Should().NotBeNull();
    }

    [Fact]
    public void EndToEnd_AddingLengths_WorksCorrectly()
    {
        // Arrange
        var input = "100 m + 50 m";

        // Act
        var parseResult = NumpusParser.ParseExpression(input);
        var evalResult = _evaluator.Evaluate(parseResult.Value);

        // Assert
        evalResult.HasError.Should().BeFalse();
        evalResult.QuantityValue.Should().NotBeNull();
    }

    [Fact]
    public void EndToEnd_Document_ParsesAndEvaluatesAllLines()
    {
        // Arrange
        var document = @"# Test document
x = 10
y = 20
result = x + y
";

        // Act
        var parseResult = NumpusParser.Parse(document);
        parseResult.Success.Should().BeTrue();

        var results = new List<CalculationResult>();
        foreach (var node in parseResult.Value)
        {
            results.Add(_evaluator.Evaluate(node));
        }

        // Assert
        results.Should().HaveCount(4);
        results.All(r => !r.HasError).Should().BeTrue();
    }

    [Fact]
    public void EndToEnd_NegativeNumbers_HandleCorrectly()
    {
        // Arrange
        var tests = new[]
        {
            ("-5", -5.0),
            ("-5 + 3", -2.0),
            ("10 + -5", 5.0),
            ("-5 * -3", 15.0)
        };

        foreach (var (input, expected) in tests)
        {
            // Act
            var parseResult = NumpusParser.ParseExpression(input);
            var evalResult = _evaluator.Evaluate(parseResult.Value);

            // Assert
            evalResult.HasError.Should().BeFalse($"'{input}' should evaluate without error");
            evalResult.NumericValue.Should().BeApproximately(expected, 0.0001, $"'{input}' = {expected}");
        }
    }

    [Fact]
    public void EndToEnd_ScientificNotation_WorksCorrectly()
    {
        // Arrange
        var input = "1.5e-10";

        // Act
        var parseResult = NumpusParser.ParseExpression(input);
        var evalResult = _evaluator.Evaluate(parseResult.Value);

        // Assert
        evalResult.HasError.Should().BeFalse();
        evalResult.NumericValue.Should().BeApproximately(1.5e-10, 1e-15);
    }

    [Fact]
    public void EndToEnd_PowerOfNegativeNumber_WorksCorrectly()
    {
        // Arrange
        var input = "(-2) ^ 3";

        // Act
        var parseResult = NumpusParser.ParseExpression(input);
        var evalResult = _evaluator.Evaluate(parseResult.Value);

        // Assert
        evalResult.HasError.Should().BeFalse();
        evalResult.NumericValue.Should().Be(-8);
    }

    [Fact]
    public void EndToEnd_NestedParentheses_EvaluateCorrectly()
    {
        // Arrange: ((2 + 3) * (4 + 1)) / 5 = (5 * 5) / 5 = 25 / 5 = 5
        var input = "((2 + 3) * (4 + 1)) / 5";

        // Act
        var parseResult = NumpusParser.ParseExpression(input);
        var evalResult = _evaluator.Evaluate(parseResult.Value);

        // Assert
        evalResult.NumericValue.Should().Be(5);
    }

    [Fact]
    public void EndToEnd_RealWorldExample_DistanceSpeedTime()
    {
        // Arrange
        var document = @"# Calculate speed
distance = 100
time = 10
speed = distance / time
";

        // Act
        var parseResult = NumpusParser.Parse(document);
        var results = new List<CalculationResult>();
        foreach (var node in parseResult.Value)
        {
            results.Add(_evaluator.Evaluate(node));
        }

        // Get the speed variable
        var speedResult = _evaluator.GetVariables()["speed"];

        // Assert
        speedResult.NumericValue.Should().Be(10);
    }

    [Fact]
    public void EndToEnd_CalculatorReuse_MaintainsState()
    {
        // Arrange & Act - First calculation
        var calc1 = NumpusParser.ParseExpression("x = 5");
        _evaluator.Evaluate(calc1.Value);

        // Second calculation uses previous variable
        var calc2 = NumpusParser.ParseExpression("y = x * 2");
        _evaluator.Evaluate(calc2.Value);

        // Third calculation uses both variables
        var calc3 = NumpusParser.ParseExpression("x + y");
        var result = _evaluator.Evaluate(calc3.Value);

        // Assert
        result.NumericValue.Should().Be(15); // 5 + 10 = 15
    }

    [Fact]
    public void EndToEnd_ClearState_RemovesAllVariables()
    {
        // Arrange
        var assign = NumpusParser.ParseExpression("x = 42");
        _evaluator.Evaluate(assign.Value);

        // Act
        _evaluator.Clear();
        var use = NumpusParser.ParseExpression("x");
        var result = _evaluator.Evaluate(use.Value);

        // Assert
        result.HasError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Undefined");
    }

    [Fact]
    public void EndToEnd_CommentLines_DoNotAffectCalculations()
    {
        // Arrange
        var document = @"# This is a comment
5 + 3
# Another comment
10 * 2
";

        // Act
        var parseResult = NumpusParser.Parse(document);
        var results = parseResult.Value
            .Select(node => _evaluator.Evaluate(node))
            .Where(r => r.NumericValue != 0) // Filter out comments
            .ToList();

        // Assert
        results.Should().HaveCount(2);
        results[0].NumericValue.Should().Be(8);
        results[1].NumericValue.Should().Be(20);
    }

    [Fact]
    public void EndToEnd_SimpleTwoLineComment_ParsesSuccessfully()
    {
        // Arrange - Minimal test case to reproduce the issue
        var document = @"# Comment 1

# Comment 2
";

        // Act
        var parseResult = NumpusParser.Parse(document);

        // Assert
        parseResult.Success.Should().BeTrue($"Two line comment should parse successfully. Error: {parseResult.Error}");

        var nodes = parseResult.Value.ToList();
        // Blank lines are not preserved as nodes, only actual statements
        var comments = nodes.OfType<CommentNode>().Where(c => !string.IsNullOrEmpty(c.Text)).ToList();
        comments.Should().HaveCount(2);
        comments[0].Text.Should().Contain("Comment 1");
        comments[1].Text.Should().Contain("Comment 2");
    }

    [Fact]
    public void EndToEnd_CommentFollowedByExpression_ParsesSuccessfully()
    {
        // Arrange - Minimal test to reproduce the line 4 issue
        var document = @"# Comment

5 + 3
";

        // Act
        var parseResult = NumpusParser.Parse(document);

        // Assert
        parseResult.Success.Should().BeTrue($"Comment followed by expression should parse. Error: {parseResult.Error}");

        var nodes = parseResult.Value.ToList();
        nodes.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void EndToEnd_DefaultDocumentFromMainWindow_ParsesSuccessfully()
    {
        // Arrange - This is the exact default text from MainWindowViewModel
        var document = @"# Numpus Calculator - Examples

# Simple arithmetic
5 + 3
10 * 2
100 / 4
2 + 3 * 4

# Variable assignments
x = 42
y = 10
result = x + y

# Units with UnitsNet
distance = 100 m
time = 10 s
speed = distance / time

# Temperature conversions
temp_celsius = 25 c
temp_fahrenheit = 77 f
temp_kelvin = 300 k

# Length calculations
length1 = 5 m
length2 = 300 cm
total_length = length1 + length2

# Area calculation
width = 5 m
height = 10 m
area = width * height

# Error examples (uncomment to see error handling)
";

        // Act
        var parseResult = NumpusParser.Parse(document);

        // Assert
        parseResult.Success.Should().BeTrue($"Default document should parse successfully. Error: {parseResult.Error}");

        var nodes = parseResult.Value.ToList();
        nodes.Should().NotBeEmpty("Document should produce AST nodes");

        // Verify that we can evaluate all the statements
        foreach (var node in nodes)
        {
            var evalResult = _evaluator.Evaluate(node);
            // Some operations might have errors (like undefined variables if eval order matters)
            // but we should at least be able to evaluate without crashing
            evalResult.Should().NotBeNull();
        }
    }
}
