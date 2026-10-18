using DragonBoyAccountManager.Core.Game.Context;
using DragonBoyAccountManager.Core.Game.Modules;

namespace DragonBoyAccountManager.Core.Game.Actions;

public class AutoChatAction : GameAction
{
    private readonly IReadOnlyList<string> _messages;
    private readonly TimeSpan _delay;
    private readonly ChatType _chatType;
    private readonly bool _isRandom;
    private int _currentIndex;
    private DateTime _lastSentTime = DateTime.MinValue;

    public int SentCount { get; private set; }
    public string LastMessage { get; private set; } = string.Empty;
    public DateTime LastSentTime => _lastSentTime;
    public ChatType ChatType => _chatType;
    public TimeSpan Delay => _delay;
    public bool IsRandom => _isRandom;

    public event Action<string, int>? ChatSent;

    public AutoChatAction(
        IReadOnlyList<string> messages,
        TimeSpan delay,
        ChatType chatType = ChatType.Map,
        bool isRandom = false)
        : base("Auto Chat")
    {
        _messages = messages?.Where(m => !string.IsNullOrWhiteSpace(m)).ToList() ?? [];
        _delay = delay <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : delay;
        _chatType = chatType;
        _isRandom = isRandom;
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
        if (_messages.Count == 0)
        {
            return Task.FromResult(ActionResult.Failure("Danh sách nội dung chat trống."));
        }

        if (!context.IsRunning || !context.Player.IsInGame)
        {
            return Task.FromResult(ActionResult.Success("Đang chờ nhân vật vào game..."));
        }

        if (_lastSentTime != DateTime.MinValue && DateTime.UtcNow - _lastSentTime < _delay)
        {
            return Task.FromResult(ActionResult.Success("Chưa tới chu kỳ gửi tiếp theo."));
        }

        string message = _isRandom
            ? _messages[Random.Shared.Next(_messages.Count)]
            : _messages[_currentIndex++ % _messages.Count];

        bool success = context.Network.SendChat(message, _chatType);
        if (success)
        {
            _lastSentTime = DateTime.UtcNow;
            SentCount++;
            LastMessage = message;
            progress?.Report($"[AutoChat] Đã gửi: {message} ({_chatType}) [Tổng: {SentCount}]");
            ChatSent?.Invoke(message, SentCount);
            return Task.FromResult(ActionResult.Success($"Đã gửi: {message}"));
        }

        return Task.FromResult(ActionResult.Failure("Không thể gửi tin nhắn chat."));
    }
}
