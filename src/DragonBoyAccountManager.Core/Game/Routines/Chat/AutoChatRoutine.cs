using DragonBoyAccountManager.Core.Game.Actions;
using DragonBoyAccountManager.Core.Game.Context;
using DragonBoyAccountManager.Core.Game.Modules;
using DragonBoyAccountManager.Core.Game.Routines.Abstractions;

namespace DragonBoyAccountManager.Core.Game.Routines.Chat;

public class AutoChatRoutine : AutomationRoutine<AutoChatOptions>
{
    private int _currentIndex;
    private DateTime _lastSentTime = DateTime.MinValue;

    public int SentCount { get; private set; }
    public string LastMessage { get; private set; } = string.Empty;
    public DateTime LastSentTime => _lastSentTime;

    public event Action<string, int>? ChatSent;

    public AutoChatRoutine(AutoChatOptions options)
        : base("Auto Chat", options, RoutinePriority.Background, "AutoChat")
    {
        _currentIndex = 0;
    }

    public AutoChatRoutine(
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

    protected override async Task OnTickAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var messages = Options.Messages;
        if (messages.Count == 0 || !context.IsRunning || !context.Player.IsInGame)
        {
            await Task.Delay(1000, cancellationToken);
            return;
        }

        if (_lastSentTime != DateTime.MinValue && DateTime.UtcNow - _lastSentTime < Options.Delay)
        {
            var remaining = Options.Delay - (DateTime.UtcNow - _lastSentTime);
            int sleepMs = (int)Math.Clamp(remaining.TotalMilliseconds, 200, 1000);
            await Task.Delay(sleepMs, cancellationToken);
            return;
        }

        string message = Options.IsRandom
            ? messages[Random.Shared.Next(messages.Count)]
            : messages[_currentIndex++ % messages.Count];

        var chatAction = new SendChatAction(message, Options.ChatType);
        var result = await ExecuteActionAsync(chatAction, context, progress, cancellationToken);

        if (result.IsSuccess)
        {
            _lastSentTime = DateTime.UtcNow;
            SentCount++;
            LastMessage = message;
            ChatSent?.Invoke(message, SentCount);
            progress?.Report($"[Auto Chat] Đã gửi: {message} ({Options.ChatType}) [Tổng: {SentCount}]");
        }

        await Task.Delay(1000, cancellationToken);
    }
}
