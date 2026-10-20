using DragonBoyAccountManager.Core.Game.Context;
using DragonBoyAccountManager.Core.Game.Modules;

namespace DragonBoyAccountManager.Core.Game.Actions;

public sealed class AutoChatOptions
{
    public IReadOnlyList<string> Messages { get; init; } = [];
    public TimeSpan Delay { get; init; } = TimeSpan.FromSeconds(5);
    public ChatType ChatType { get; init; } = ChatType.Map;
    public bool IsRandom { get; init; }
}

public class AutoChatAction : GameAction<AutoChatOptions>
{
    private int _currentIndex;
    private DateTime _lastSentTime = DateTime.MinValue;

    public int SentCount { get; private set; }
    public string LastMessage { get; private set; } = string.Empty;
    public DateTime LastSentTime => _lastSentTime;

    public event Action<string, int>? ChatSent;

    public AutoChatAction(AutoChatOptions options) : base("Auto Chat", options) { }

    public AutoChatAction(
        IReadOnlyList<string> messages,
        TimeSpan delay,
        ChatType chatType = ChatType.Map,
        bool isRandom = false)
        : this(new AutoChatOptions
        {
            Messages = messages?.Where(m => !string.IsNullOrWhiteSpace(m)).ToList() ?? [],
            Delay = delay <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : delay,
            ChatType = chatType,
            IsRandom = isRandom
        })
    { }

    public void UpdateConfig(
        IReadOnlyList<string> messages,
        TimeSpan delay,
        ChatType chatType = ChatType.Map,
        bool isRandom = false)
    {
        UpdateOptions(new AutoChatOptions
        {
            Messages = messages?.Where(m => !string.IsNullOrWhiteSpace(m)).ToList() ?? [],
            Delay = delay <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : delay,
            ChatType = chatType,
            IsRandom = isRandom
        });
    }

    protected override void OnOptionsChanged(AutoChatOptions previousOptions, AutoChatOptions newOptions)
    {
        _currentIndex = 0;
    }

    public void Reset()
    {
        _currentIndex = 0;
        SentCount = 0;
        LastMessage = string.Empty;
        _lastSentTime = DateTime.MinValue;
    }

    protected override Task<ActionResult> OnExecuteAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var messages = Options.Messages;
        if (messages.Count == 0)
        {
            return Task.FromResult(ActionResult.Failure("Danh sách nội dung chat trống."));
        }

        if (!context.IsRunning || !context.Player.IsInGame)
        {
            return Task.FromResult(ActionResult.Success("Đang chờ nhân vật vào game..."));
        }

        if (_lastSentTime != DateTime.MinValue && DateTime.UtcNow - _lastSentTime < Options.Delay)
        {
            return Task.FromResult(ActionResult.Success("Chưa tới chu kỳ gửi tiếp theo."));
        }

        string message = Options.IsRandom
            ? messages[Random.Shared.Next(messages.Count)]
            : messages[_currentIndex++ % messages.Count];

        bool success = context.Network.SendChat(message, Options.ChatType);
        if (success)
        {
            _lastSentTime = DateTime.UtcNow;
            SentCount++;
            LastMessage = message;
            progress?.Report($"[AutoChat] Đã gửi: {message} ({Options.ChatType}) [Tổng: {SentCount}]");
            ChatSent?.Invoke(message, SentCount);
            return Task.FromResult(ActionResult.Success($"Đã gửi: {message}"));
        }

        return Task.FromResult(ActionResult.Failure("Không thể gửi tin nhắn chat."));
    }
}
