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

    public void Deconstruct(out bool isSuccess, out string message)
    {
        isSuccess = IsSuccess;
        message = Message;
    }

    public static implicit operator (bool Success, string Message)(Result result) => (result.IsSuccess, result.Message);
}
