using System;
using System.Linq;
using Numpus.Compiler;
using Pidgin;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;

namespace ParserDebug;

internal static class Program
{
    private static void Main()
    {
        Test("Assignment with newline", "x = 42\n");
        Test("Assignment without newline", "x = 42");
        Test("Two assignments", "x = 42\ny = 5\n");
        Test("Single comment", "# This is a comment\n");
        Test("Comment no newline", "# Comment 1");
        Test("Mixed content", "# Calculator\nx = 5\ny = 3\nx + y\n");
        Test("Comment blank line comment", "# Comment 1\n\n# Comment 2\n");
        Test("Blank line only", "\n");
        Test("Two blank lines", "\n\n");

        var defaultDocField = typeof(Numpus.ViewModels.MainWindowViewModel)
            .GetField("_defaultDocument", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (defaultDocField?.GetValue(null) is string defaultDoc)
        {
            Test("Default Document", defaultDoc);

            Console.WriteLine("\n--- Default Document Lines ---");
            var defaultLines = defaultDoc.Split('\n');
            for (var i = 0; i < defaultLines.Length; i++)
            {
                Console.WriteLine($"{i + 1,2}: {defaultLines[i]}");
            }
        }

        var inlineWhitespace = OneOf(' ', '\t').SkipMany();
        var requiredInlineWhitespace = OneOf(' ', '\t').AtLeastOnce().Then(Return(Unit.Value));
        var anyWhitespace = Whitespace.SkipMany();
        var newline = Try(String("\r\n")).Then(Return(Unit.Value)).Or(Char('\n').Then(Return(Unit.Value)));
        var semicolonSeparator = Char(';').Then(Return(Unit.Value));
        var statementSeparator =
            from _ in inlineWhitespace
            from sep in newline.Or(semicolonSeparator)
            from __ in inlineWhitespace
            select Unit.Value;
        Parser<char, T> Tok<T>(Parser<char, T> parser) => parser.Between(inlineWhitespace, inlineWhitespace);

        var commentParser =
            from _ in Char('#')
            from text in Any.Where(ch => ch != '\n' && ch != '\r').ManyString()
            select new CommentNode(text.Trim(), 0, 0);

        var numberLiteral = Digit.AtLeastOnceString();
        var numberNodeParser =
            from literal in numberLiteral
            select (AstNode)new NumberNode(double.Parse(literal), null, 0, 0);

        Parser<char, AstNode> expression = null!;
        expression = Rec<char, AstNode>(_ => Tok(numberNodeParser));

        var commentStatement =
            from _ in inlineWhitespace
            from comment in commentParser
            select (AstNode)comment;

        var statement = Try(commentStatement).Or(expression);

        var statementList =
            from _ in statementSeparator.Many()
            from statements in statement
                .Before(statementSeparator.Many())
                .Many()
            from __ in statementSeparator.Many()
            select statements.ToList();

        var programParser = inlineWhitespace.Then(statementList).Before(statementSeparator.Many()).Before(inlineWhitespace).Before(End);

        Console.WriteLine("--- commentStatement with trailing separators ---");
        var commentWithSep = commentStatement.Before(statementSeparator.Many()).Parse("# hi\n");
        Console.WriteLine($"Success: {commentWithSep.Success}");
        Console.WriteLine($"Error: {commentWithSep.Error}");

        Console.WriteLine("--- statementList parser ---");
        var programResult = programParser.Parse("# hi\n");
        Console.WriteLine($"Success: {programResult.Success}");
        Console.WriteLine($"Error: {programResult.Error}");
        if (programResult.Success)
        {
            Console.WriteLine($"Count: {programResult.Value.Count}");
        }

        Console.WriteLine("\n--- DETAILED TRACE ---");
        Console.WriteLine("Step 1: Try commentStatement on '# hi'");
        var step2 = commentStatement.Parse("# hi");
        Console.WriteLine($"Success: {step2.Success}");

        Console.WriteLine("Step 2: Try commentStatement on '# hi\\n'");
        try
        {
            var step3 = commentStatement.Parse("# hi\n");
            Console.WriteLine($"Success: {step3.Success}, Consumed: Comment");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }

        Console.WriteLine("Step 3: Try statementList");
        var step4 = statementList.Parse("# hi\n");
        Console.WriteLine($"Success: {step4.Success}");
        if (!step4.Success)
        {
            Console.WriteLine($"Error: {step4.Error}");
        }

        Console.WriteLine("Step 4: Try statementList.Before(End)");
        var step5 = statementList.Before(End).Parse("# hi\n");
        Console.WriteLine($"Success: {step5.Success}");
        if (!step5.Success)
        {
            Console.WriteLine($"Error: {step5.Error}");
        }

        Console.WriteLine("Step 5: Check what statementList consumed");
        var step6 = statementList.ParseOrThrow("# hi\n");
        Console.WriteLine($"Parsed {step6.Count} statements");

        Console.WriteLine("Step 6: Try just commentStatement.Before(statementSeparator.Many())");
        var step7 = commentStatement.Before(statementSeparator.Many()).Parse("# hi\n");
        Console.WriteLine($"Success: {step7.Success}");

        Console.WriteLine("Step 7: Try commentStatement.Before(statementSeparator.Many()).Before(End)");
        var step8 = commentStatement.Before(statementSeparator.Many()).Before(End).Parse("# hi\n");
        Console.WriteLine($"Success: {step8.Success}");
        if (!step8.Success)
        {
            Console.WriteLine($"Error: {step8.Error}");
        }

        Console.WriteLine("Step 8: Try statementSeparator on '\\n'");
        var step9 = statementSeparator.Parse("\n");
        Console.WriteLine($"Success: {step9.Success}");
        if (!step9.Success)
        {
            Console.WriteLine($"Error: {step9.Error}");
        }

        Console.WriteLine("Step 9: Try statementSeparator.Many() on '\\n'");
        var step10 = statementSeparator.Many().Parse("\n");
        Console.WriteLine($"Success: {step10.Success}");
        if (!step10.Success)
        {
            Console.WriteLine($"Error: {step10.Error}");
        }

        Console.WriteLine("Step 10: Try statementSeparator.Many().Before(End) on '\\n'");
        var step11 = statementSeparator.Many().Before(End).Parse("\n");
        Console.WriteLine($"Success: {step11.Success}");
        if (!step11.Success)
        {
            Console.WriteLine($"Error: {step11.Error}");
        }

        Console.WriteLine("Step 11: Try numberNodeParser.Before(statementSeparator.Many()) on '123\\n'");
        var step12 = numberNodeParser.Before(statementSeparator.Many()).Parse("123\n");
        Console.WriteLine($"Success: {step12.Success}");
        if (!step12.Success)
        {
            Console.WriteLine($"Error: {step12.Error}");
        }

        Console.WriteLine("Step 12: Try Tok(commentParser).Before(statementSeparator.Many()) on '# hi\\n'");
        var tokComment = Tok(commentParser);
        var step13 = tokComment.Before(statementSeparator.Many()).Parse("# hi\n");
        Console.WriteLine($"Success: {step13.Success}");
        if (!step13.Success)
        {
            Console.WriteLine($"Error: {step13.Error}");
        }
    }

    private static void Test(string label, string input)
    {
        Console.WriteLine($"--- {label} ---");
        Console.WriteLine($"Input: {input.Replace("\n", "\\n").Replace("\r", "\\r")}");
        var result = NumpusParser.Parse(input);
        Console.WriteLine($"Success: {result.Success}");
        Console.WriteLine($"Error: {result.Error}");
        if (result.Success && result.Value != null)
        {
            var nodes = result.Value.ToList();
            Console.WriteLine($"Nodes: {nodes.Count}");
            foreach (var node in nodes)
            {
                Console.WriteLine(node.GetType().Name);
            }
        }
    }
}
