using System.Collections.Generic;

namespace Numpus.Compiler;

public abstract record AstNode(int Line, int Column);

public sealed record NumberNode(double Value, string? Unit, int Line, int Column) : AstNode(Line, Column);

public sealed record VariableNode(string Name, int Line, int Column) : AstNode(Line, Column);

public sealed record CommentNode(string Text, int Line, int Column) : AstNode(Line, Column);

public sealed record AssignmentNode(string Name, AstNode Value, int Line, int Column) : AstNode(Line, Column);

public sealed record UnaryOpNode(string Operator, AstNode Operand, int Line, int Column) : AstNode(Line, Column);

public sealed record BinaryOpNode(AstNode Left, string Operator, AstNode Right, int Line, int Column) : AstNode(Line, Column);

public sealed record FunctionDefinitionNode(string Name, IReadOnlyList<string> Parameters, AstNode Body, int Line, int Column) : AstNode(Line, Column);

public sealed record FunctionCallNode(string Name, IReadOnlyList<AstNode> Arguments, int Line, int Column) : AstNode(Line, Column);
