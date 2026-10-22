using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Behaviors.Combat;

public sealed class AutoChatOptions
{
    public IReadOnlyList<string> Messages { get; init; } = [];
    public TimeSpan Delay { get; init; } = TimeSpan.FromSeconds(5);
    public bool IsGlobal { get; init; }
    public bool IsRandom { get; init; }
}

public sealed class AutoChatBehavior(AutoChatOptions options)
    : AutomationBehavior<AutoChatOptions>("Auto Chat", options, BehaviorId)
{
    public const string BehaviorId = "AutoChat";

    private DateTime _lastChatTime = DateTime.MinValue;
    private int _messageIndex;
    private readonly Random _random = new();

    protected override async Task OnTickAsync(
        PlayerSnapshot player,
        WorldSnapshot world,
        MapSnapshot map,
        ICommandDispatcher dispatcher,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
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
            msg = Options.Messages[_random.Next(Options.Messages.Count)];
        }
        else
        {
            msg = Options.Messages[_messageIndex % Options.Messages.Count];
            _messageIndex++;
        }

        _lastChatTime = DateTime.UtcNow;
        var result = await dispatcher.DispatchAsync(new SendChatCommand(msg, Options.IsGlobal), cancellationToken);
        if (result.IsSuccess)
        {
            progress?.Report($"[Auto Chat] {msg}");
        }
    }
}
