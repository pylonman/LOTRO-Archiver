namespace LotroArchiver;

public readonly record struct Result<T>(T? Value, string? Error, bool IsSuccess)
{
    public static Result<T> Success(T value) => new(value, null, true);
    public static Result<T> Failure(string error) => new(default, error, false);
}

public readonly record struct Result(string? Error, bool IsSuccess)
{
    public static Result Success() => new(null, true);
    public static Result Failure(string error) => new(error, false);
}