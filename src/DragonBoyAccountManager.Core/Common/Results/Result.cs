namespace DragonBoyAccountManager.Core.Common.Results;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Message { get; }
    public Exception? Exception { get; }

    protected Result(bool isSuccess, string message, Exception? exception = null)
    {
        IsSuccess = isSuccess;
        Message = message;
        Exception = exception;
    }

    public static Result Success(string message = "") => new(true, message);
    public static Result Failure(string message, Exception? exception = null) => new(false, message, exception);

    public static Result<T> Success<T>(T value, string message = "") => new(value, true, message);
    public static Result<T> Failure<T>(string message, Exception? exception = null) => new(default, false, message, exception);

    public void Deconstruct(out bool isSuccess, out string message)
    {
        isSuccess = IsSuccess;
        message = Message;
    }

    public static implicit operator (bool Success, string Message)(Result result) => (result.IsSuccess, result.Message);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    public T Value => IsSuccess
        ? (_value ?? throw new InvalidOperationException("Value is null in a successful result."))
        : throw new InvalidOperationException($"Cannot access Value on a failed result: {Message}", Exception);

    public T? ValueOrDefault => _value;

    internal Result(T? value, bool isSuccess, string message, Exception? exception = null)
        : base(isSuccess, message, exception)
    {
        _value = value;
    }

    public static implicit operator Result<T>(T value) => Success(value);
}
