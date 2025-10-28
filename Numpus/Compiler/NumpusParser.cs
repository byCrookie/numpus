using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pidgin;
using Pidgin.Expression;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;

namespace Numpus.Compiler;

public static class NumpusParser
{
    private static readonly Parser<char, Unit> _skipWhitespace = Whitespace.SkipMany();

    private static Parser<char, T> Tok<T>(Parser<char, T> parser) => parser.Between(_skipWhitespace, _skipWhitespace);

    private static readonly Parser<char, string> _identifier = Map(
        (first, rest) => first + rest,
        Letter.Or(Char('_')),
        LetterOrDigit.Or(Char('_')).ManyString()
    );

    private static readonly Parser<char, IReadOnlyList<string>> _parameterList =
        Tok(Char('('))
            .Then(
                Tok(_identifier)
                    .Separated(Tok(Char(',')))
                    .Optional()
                    .Select(option => option.HasValue
                        ? (IReadOnlyList<string>)option.Value.ToArray()
                        : Array.Empty<string>())
            )
            .Before(Tok(Char(')')));

    private static readonly Parser<char, string> _unitIdentifier =
        from first in Letter
        from rest in LetterOrDigit.Or(OneOf("_/-")).Or(Char('^')).ManyString()
        select first + rest;

    private static readonly Parser<char, string?> _fractionPart =
        Try(
            from _ in Char('.')
            from digits in Digit.AtLeastOnceString()
            select (string?)digits
        ).Or(Return<string?>(null));

    private static readonly Parser<char, ExponentParts?> _exponentPart =
        Try(
            from marker in OneOf('e', 'E')
            from signOpt in OneOf('+', '-').Optional()
            from digits in Digit.AtLeastOnceString()
            select new ExponentParts(marker, signOpt.HasValue ? signOpt.Value : null, digits)
        )
        .Select(exp => (ExponentParts?)exp)
        .Or(Return<ExponentParts?>(null));

    private static readonly Parser<char, string> _numberLiteral =
        from whole in Digit.AtLeastOnceString()
        from fraction in _fractionPart
        from exponent in _exponentPart
        select CombineNumberParts(whole, fraction, exponent);

    private static readonly Parser<char, string?> _unitPart =
        Try(
            from _ in Whitespace.AtLeastOnce()
            from ident in _unitIdentifier
            select (string?)ident
        )
        .Or(Try(_unitIdentifier).Select(ident => (string?)ident))
        .Or(Return<string?>(null));

    private static readonly Parser<char, AstNode> _numberNodeParser =
        from literal in _numberLiteral
        from unit in _unitPart
        let value = double.Parse(literal, CultureInfo.InvariantCulture)
        let unitText = string.IsNullOrWhiteSpace(unit) ? null : unit
        select (AstNode)new NumberNode(value, unitText, 0, 0);

    private static readonly Parser<char, AstNode> _variableNodeParser =
        _identifier.Select(name => (AstNode)new VariableNode(name, 0, 0));

    private static readonly Parser<char, AstNode> _expressionCore = BuildExpressionCore();

    private static readonly Parser<char, AstNode> _functionDefinitionExpression =
        Try(
            from name in Tok(_identifier)
            from parameters in _parameterList
            from _ in Tok(Char('='))
            from body in _expressionCore
            select (AstNode)new FunctionDefinitionNode(name, parameters, body, 0, 0)
        );

    private static readonly Parser<char, AstNode> _assignmentExpression =
        Try(
            from name in Tok(_identifier)
            from _ in Tok(Char('='))
            from value in _expressionCore
            select (AstNode)new AssignmentNode(name, value, 0, 0)
        );

    private static readonly Parser<char, AstNode> _expressionWithAssignment =
        _functionDefinitionExpression.Or(_assignmentExpression).Or(_expressionCore);

    public static ParseResult<AstNode> ParseExpression(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var parser = _skipWhitespace
            .Then(_expressionWithAssignment)
            .Before(_skipWhitespace)
            .Before(End);

        try
        {
            var value = parser.ParseOrThrow(input);
            return ParseResult<AstNode>.FromSuccess(value);
        }
        catch (ParseException ex)
        {
            return ParseResult<AstNode>.FromError(ex.Message);
        }
    }

    public static ParseResult<IReadOnlyList<AstNode>> Parse(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input))
        {
            return ParseResult<IReadOnlyList<AstNode>>.FromSuccess(Array.Empty<AstNode>());
        }

        var normalized = input.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var nodes = new List<AstNode>();

        for (var index = 0; index < lines.Length; index++)
        {
            var rawLine = lines[index];
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var trimmedLine = rawLine.TrimStart();
            if (trimmedLine.StartsWith('#'))
            {
                var commentText = trimmedLine[1..].Trim();
                nodes.Add(new CommentNode(commentText, index + 1, rawLine.IndexOf('#') + 1));
                continue;
            }

            var expressionResult = ParseExpression(rawLine);
            if (!expressionResult.Success)
            {
                var error = expressionResult.Error ?? "Unknown parse error.";
                return ParseResult<IReadOnlyList<AstNode>>.FromError($"Line {index + 1}: {error}");
            }

            nodes.Add(expressionResult.Value!);
        }

        return ParseResult<IReadOnlyList<AstNode>>.FromSuccess(nodes);
    }

    private static Parser<char, AstNode> BuildExpressionCore()
    {
        return ExpressionParser.Build<char, AstNode>(expr =>
        (
            OneOf(
                Tok(_numberNodeParser),
                Tok(Try(ParseFunctionCall(expr))),
                Tok(_variableNodeParser),
                Tok(Char('(')).Then(expr).Before(Tok(Char(')')))
            ),
            new[]
            {
                Operator.Prefix(Tok(Char('+')).Select(_ => (Func<AstNode, AstNode>)(operand => new UnaryOpNode("+", operand, 0, 0)))),
                Operator.Prefix(Tok(Char('-')).Select(_ => (Func<AstNode, AstNode>)(operand => new UnaryOpNode("-", operand, 0, 0)))),
                Operator.InfixR(Tok(Char('^')).Select(_ => (Func<AstNode, AstNode, AstNode>)((left, right) => new BinaryOpNode(left, "^", right, 0, 0)))),
                Operator.InfixL(Tok(Char('*')).Select(_ => (Func<AstNode, AstNode, AstNode>)((left, right) => new BinaryOpNode(left, "*", right, 0, 0)))),
                Operator.InfixL(Tok(Char('/')).Select(_ => (Func<AstNode, AstNode, AstNode>)((left, right) => new BinaryOpNode(left, "/", right, 0, 0)))),
                Operator.InfixL(Tok(Char('+')).Select(_ => (Func<AstNode, AstNode, AstNode>)((left, right) => new BinaryOpNode(left, "+", right, 0, 0)))),
                Operator.InfixL(Tok(Char('-')).Select(_ => (Func<AstNode, AstNode, AstNode>)((left, right) => new BinaryOpNode(left, "-", right, 0, 0))))
            }
        ));
    }

    private static Parser<char, AstNode> ParseFunctionCall(Parser<char, AstNode> expression)
    {
        return
            from name in Tok(_identifier)
            from args in ParseArgumentList(expression)
            select (AstNode)new FunctionCallNode(name, args, 0, 0);
    }

    private static Parser<char, IReadOnlyList<AstNode>> ParseArgumentList(Parser<char, AstNode> expression)
    {
        return Tok(Char('('))
            .Then(
                expression
                    .Separated(Tok(Char(',')))
                    .Optional()
                    .Select(option => option.HasValue
                        ? (IReadOnlyList<AstNode>)option.Value.ToArray()
                        : Array.Empty<AstNode>())
            )
            .Before(Tok(Char(')')));
    }

    private static string CombineNumberParts(string whole, string? fraction, ExponentParts? exponent)
    {
        var result = whole;
        if (!string.IsNullOrEmpty(fraction))
        {
            result += "." + fraction;
        }

        if (exponent is { } exp)
        {
            var sign = exp.Sign.HasValue ? exp.Sign.Value.ToString() : string.Empty;
            result += exp.Marker + sign + exp.Digits;
        }

        return result;
    }

    private readonly record struct ExponentParts(char Marker, char? Sign, string Digits);
}
