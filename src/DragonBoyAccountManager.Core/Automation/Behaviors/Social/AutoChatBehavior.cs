using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Automation.Behaviors.Social;

public sealed class AutoChatBehavior(AutoChatOptions options)
    : AutomationBehavior<AutoChatOptions>("Auto Chat", options, BehaviorId)
{
    public const string BehaviorId = "AutoChat";

    public override BehaviorExecutionMode ExecutionMode => BehaviorExecutionMode.Background;

    private DateTime _lastChatTime = DateTime.MinValue;
    private int _messageIndex;

    protected override async Task OnTickAsync(
        ISessionContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var player = context.CurrentPlayer;
        if (!player.IsInGame || Options.Messages.Count == 0)
        {
            await Task.Delay(500, cancellationToken);
            return;
        }

        if (DateTime.UtcNow - _lastChatTime < Options.Delay)
        {
            return;
        }

        string msg;
        if (Options.IsRandom)
        {
            msg = Options.Messages[Random.Shared.Next(Options.Messages.Count)];
        }
        else
        {
            msg = Options.Messages[_messageIndex % Options.Messages.Count];
            _messageIndex++;
        }

        _lastChatTime = DateTime.UtcNow;
        var result = await context.Dispatcher.DispatchAsync(new SendChatCommand(msg, Options.IsGlobal), cancellationToken);
        if (result.IsSuccess)
        {
            progress?.Report($"[Auto Chat] {msg}");
        }
    }
}
