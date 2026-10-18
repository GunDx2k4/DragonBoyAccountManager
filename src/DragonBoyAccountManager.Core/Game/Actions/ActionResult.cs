using DragonBoyAccountManager.Core.Game.Context;

namespace DragonBoyAccountManager.Core.Game.Actions;

public enum ActionStatus
{
    Success,
    Failure,
    Running
}

public record ActionResult(ActionStatus Status, string Message = "", Exception? Exception = null)
{
    public static ActionResult Success(string msg = "") => new(ActionStatus.Success, msg);
    public static ActionResult Failure(string msg = "", Exception? ex = null) => new(ActionStatus.Failure, msg, ex);
    public static ActionResult Running(string msg = "") => new(ActionStatus.Running, msg);

    public bool IsSuccess => Status == ActionStatus.Success;
    public bool IsFailure => Status == ActionStatus.Failure;
    public bool IsRunning => Status == ActionStatus.Running;

    public static implicit operator bool(ActionResult result) => result != null && result.IsSuccess;
}

public interface IGameAction
{
    string Name { get; }
    Task<ActionResult> ExecuteAsync(IGameContext context, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}
