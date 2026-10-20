namespace DragonBoyAccountManager.Core.Automation.Actions.Abstractions;

public readonly record struct ActionExecutionReport(
    string ActionName,
    ActionStatus Status,
    string Message,
    DateTime Timestamp)
{
    public static ActionExecutionReport Running(string actionName, string? message = null) =>
        new(actionName, ActionStatus.Running, !string.IsNullOrWhiteSpace(message) ? message : actionName, DateTime.UtcNow);

    public static ActionExecutionReport Completed(string actionName, ActionResult result) =>
        new(actionName, result.Status, !string.IsNullOrWhiteSpace(result.Message) ? result.Message : actionName, DateTime.UtcNow);

    public static ActionExecutionReport Idle() =>
        new(string.Empty, ActionStatus.Success, string.Empty, DateTime.UtcNow);
}
