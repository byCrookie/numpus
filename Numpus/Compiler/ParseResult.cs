namespace Numpus.Compiler;

public sealed class ParseResult<T>
{
    private ParseResult(bool success, T? value, string? error)
    {
        Success = success;
        Value = value;
        Error = error;
    }

    public bool Success { get; }

    public T? Value { get; }

    public string? Error { get; }

    public static ParseResult<T> FromSuccess(T value) => new(true, value, null);

    public static ParseResult<T> FromError(string error) => new(false, default, error);
}
