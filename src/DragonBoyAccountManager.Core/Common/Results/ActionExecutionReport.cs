namespace DragonBoyAccountManager.Core.Common.Results;

public enum ActionStatus
{
    Idle,
    Running,
    Success,
    Failure,
    Cancelled
}

public readonly record struct ActionExecutionReport(
    string ActionName,
    ActionStatus Status,
    string Message,
    DateTime Timestamp)
{
    public static ActionExecutionReport Running(string actionName, string? message = null) =>
        new(actionName, ActionStatus.Running, !string.IsNullOrWhiteSpace(message) ? message : actionName, DateTime.UtcNow);

    public static ActionExecutionReport Success(string actionName, string? message = null) =>
        new(actionName, ActionStatus.Success, !string.IsNullOrWhiteSpace(message) ? message : actionName, DateTime.UtcNow);

    public static ActionExecutionReport Failure(string actionName, string? message = null) =>
        new(actionName, ActionStatus.Failure, !string.IsNullOrWhiteSpace(message) ? message : actionName, DateTime.UtcNow);

    public static ActionExecutionReport Idle() =>
        new(string.Empty, ActionStatus.Idle, string.Empty, DateTime.UtcNow);
}
