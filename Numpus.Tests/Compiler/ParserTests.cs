using FluentAssertions;
using Numpus.Compiler;
using Xunit;

namespace Numpus.Tests.Compiler;

public class ParserTests
{
    [Fact]
    public void ParseExpression_SimpleNumber_ReturnsNumberNode()
    {
        // Arrange
        var input = "42";

        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeTrue();
        var number = result.Value.Should().BeOfType<NumberNode>().Subject;
        number.Value.Should().Be(42);
        number.Unit.Should().BeNull();
    }

    [Fact]
    public void ParseExpression_NumberWithUnit_ReturnsNumberNodeWithUnit()
    {
        // Arrange
        var input = "100 m";

        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeTrue();
        var number = result.Value.Should().BeOfType<NumberNode>().Subject;
        number.Value.Should().Be(100);
        number.Unit.Should().Be("m");
    }

    [Fact]
    public void ParseExpression_SimpleAddition_ReturnsBinaryOpNode()
    {
        // Arrange
        var input = "5 + 3";

        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeTrue();
        var binOp = result.Value.Should().BeOfType<BinaryOpNode>().Subject;
        binOp.Operator.Should().Be("+");
        binOp.Left.Should().BeOfType<NumberNode>();
        binOp.Right.Should().BeOfType<NumberNode>();
    }

    [Fact]
    public void ParseExpression_Multiplication_ReturnsBinaryOpNode()
    {
        // Arrange
        var input = "10 * 2";

        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeTrue();
        var binOp = result.Value.Should().BeOfType<BinaryOpNode>().Subject;
        binOp.Operator.Should().Be("*");
    }

    [Fact]
    public void ParseExpression_Power_ReturnsBinaryOpNode()
    {
        // Arrange
        var input = "2 ^ 3";

        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeTrue();
        var binOp = result.Value.Should().BeOfType<BinaryOpNode>().Subject;
        binOp.Operator.Should().Be("^");
    }

    [Fact]
    public void ParseExpression_ParenthesesChangePrecedence_ReturnsCorrectStructure()
    {
        // Arrange
        var input = "(5 + 3) * 2";

        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeTrue();
        var multiply = result.Value.Should().BeOfType<BinaryOpNode>().Subject;
        multiply.Operator.Should().Be("*");
        multiply.Left.Should().BeOfType<BinaryOpNode>();
        var add = (BinaryOpNode)multiply.Left;
        add.Operator.Should().Be("+");
    }

    [Fact]
    public void ParseExpression_OperatorPrecedence_MultipliesBeforeAdding()
    {
        // Arrange
        var input = "2 + 3 * 4";

        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeTrue();
        var add = result.Value.Should().BeOfType<BinaryOpNode>().Subject;
        add.Operator.Should().Be("+");
        add.Right.Should().BeOfType<BinaryOpNode>();
        var multiply = (BinaryOpNode)add.Right;
        multiply.Operator.Should().Be("*");
    }

    [Fact]
    public void ParseExpression_PowerIsRightAssociative_ReturnsCorrectStructure()
    {
        // Arrange
        var input = "2 ^ 3 ^ 2"; // Should parse as 2 ^ (3 ^ 2) = 2 ^ 9 = 512

        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeTrue();
        var power = result.Value.Should().BeOfType<BinaryOpNode>().Subject;
        power.Operator.Should().Be("^");
        power.Right.Should().BeOfType<BinaryOpNode>();
    }

    [Fact]
    public void ParseExpression_Variable_ReturnsVariableNode()
    {
        // Arrange
        var input = "distance";

        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeTrue();
        var variable = result.Value.Should().BeOfType<VariableNode>().Subject;
        variable.Name.Should().Be("distance");
    }

    [Fact]
    public void Parse_Assignment_ReturnsAssignmentNode()
    {
        // Arrange
        var input = "x = 42\n";

        // Act
        var result = NumpusParser.Parse(input);

        // Assert
        result.Success.Should().BeTrue();
        var nodes = result.Value!.ToList();
        nodes.Should().HaveCount(1);
        var assignment = nodes[0].Should().BeOfType<AssignmentNode>().Subject;
        assignment.Name.Should().Be("x");
        assignment.Value.Should().BeOfType<NumberNode>();
    }

    [Fact]
    public void ParseExpression_FunctionDefinition_ReturnsFunctionDefinitionNode()
    {
        // Arrange
        var input = "double(x) = x * 2";

        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeTrue();
        var definition = result.Value.Should().BeOfType<FunctionDefinitionNode>().Subject;
        definition.Name.Should().Be("double");
        definition.Parameters.Should().ContainSingle().Which.Should().Be("x");
        definition.Body.Should().BeOfType<BinaryOpNode>();
    }

    [Fact]
    public void ParseExpression_FunctionCall_ReturnsFunctionCallNode()
    {
        // Arrange
        var input = "double(5)";

        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeTrue();
        var call = result.Value.Should().BeOfType<FunctionCallNode>().Subject;
        call.Name.Should().Be("double");
        call.Arguments.Should().HaveCount(1);
        call.Arguments[0].Should().BeOfType<NumberNode>();
    }

    [Fact]
    public void ParseExpression_FunctionCallWithoutArguments_ReturnsFunctionCallNode()
    {
        // Arrange
        var input = "now()";

        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeTrue();
        var call = result.Value.Should().BeOfType<FunctionCallNode>().Subject;
        call.Name.Should().Be("now");
        call.Arguments.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MultipleLines_ReturnsMultipleNodes()
    {
        // Arrange
        var input = "5 + 3\n10 * 2\n";

        // Act
        var result = NumpusParser.Parse(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_CommentLine_ReturnsCommentNode()
    {
        // Arrange
        var input = "# This is a comment\n";

        // Act
        var result = NumpusParser.Parse(input);

        // Assert
        result.Success.Should().BeTrue();
        var nodes = result.Value!.ToList();
        nodes.Should().HaveCount(1);
        var comment = nodes[0].Should().BeOfType<CommentNode>().Subject;
        comment.Text.Should().Contain("This is a comment");
    }

    [Fact]
    public void ParseExpression_UnaryPlus_ReturnsUnaryOpNode()
    {
        // Arrange
        var input = "+42";

        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeTrue();
        var unary = result.Value.Should().BeOfType<UnaryOpNode>().Subject;
        unary.Operator.Should().Be("+");
    }

    [Fact]
    public void ParseExpression_UnaryMinus_ReturnsUnaryOpNode()
    {
        // Arrange
        var input = "-42";

        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeTrue();
        var unary = result.Value.Should().BeOfType<UnaryOpNode>().Subject;
        unary.Operator.Should().Be("-");
    }

    [Fact]
    public void ParseExpression_ComplexExpression_ParsesCorrectly()
    {
        // Arrange
        var input = "(5 + 3) * 2 ^ 3 - 10 / 2";

        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().BeOfType<BinaryOpNode>();
    }

    [Fact]
    public void Parse_MixedContentWithComments_ParsesAllLines()
    {
        // Arrange
        var input = @"# Calculator
x = 5
y = 3
x + y
";

        // Act
        var result = NumpusParser.Parse(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().HaveCount(4);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("5 + 3")]
    [InlineData("5 * 3 + 2")]
    [InlineData("(5 + 3) * 2")]
    [InlineData("2 ^ 3 ^ 2")]
    [InlineData("100 m")]
    [InlineData("distance = 100 km")]
    [InlineData("-42")]
    [InlineData("double(x) = x * 2")]
    [InlineData("sqrt(4)")]
    public void ParseExpression_ValidExpressions_ParseSuccessfully(string input)
    {
        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeTrue($"'{input}' should parse successfully");
    }

    [Theory]
    [InlineData("")]
    [InlineData("5 +")]
    [InlineData("* 5")]
    [InlineData("(5 + 3")]
    [InlineData("5 + 3)")]
    public void ParseExpression_InvalidExpressions_ReturnsError(string input)
    {
        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeFalse($"'{input}' should not parse successfully");
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ParseExpression_DivisionByZeroSyntax_StillParsesSuccessfully()
    {
        // Arrange
        var input = "10 / 0";

        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert - parsing should succeed, evaluation will fail
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void ParseExpression_NestedParentheses_ParsesCorrectly()
    {
        // Arrange
        var input = "((5 + 3) * (2 + 4)) / 2";

        // Act
        var result = NumpusParser.ParseExpression(input);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Parse_EmptyDocument_ReturnsEmptyResult()
    {
        // Arrange
        var input = "";

        // Act
        var result = NumpusParser.Parse(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public void Parse_OnlyWhitespace_ReturnsEmptyResult()
    {
        // Arrange
        var input = "   \n\n   \n";

        // Act
        var result = NumpusParser.Parse(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
