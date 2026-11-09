using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Automation.Behaviors.Social;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.GUI.Presentation.Features.Accounts.Items;
using DragonBoyAccountManager.GUI.Services.Dispatching;
using DragonBoyAccountManager.GUI.Services.Engine;
using DragonBoyAccountManager.GUI.Services.Selection;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto;

public sealed partial class AutoChatViewModel : AutoFeatureViewModelBase
{
    public override string BehaviorId => AutoChatBehavior.BehaviorId;

    [ObservableProperty]
    private string _chatText = "Chào mọi người!";

    [ObservableProperty]
    private int _chatDelaySeconds = 5;

    [ObservableProperty]
    private int _selectedChatTypeIndex;

    [ObservableProperty]
    private bool _isRandomChat;

    [ObservableProperty]
    private int _chatSentCount;

    [ObservableProperty]
    private string _lastChatSentTime = string.Empty;

    public AutoChatViewModel(
        IGameEngineService gameEngine,
        IAccountStorageService storageService,
        IActiveSelectionService? selectionService = null,
        IDispatcherService? dispatcher = null)
        : base(gameEngine, storageService, selectionService, dispatcher)
    {
    }

    protected override void LoadAccountCore(AccountItemViewModel account)
    {
        var chat = account.AutoSettings.GetBehaviorOptionsOrDefault<AutoChatOptions>(BehaviorId);
        ChatText = chat.Messages is { Count: > 0 }
            ? string.Join(Environment.NewLine, chat.Messages)
            : "Chào mọi người!";
        int delaySec = (int)Math.Round(chat.Delay.TotalSeconds);
        ChatDelaySeconds = delaySec <= 0 ? 5 : delaySec;
        SelectedChatTypeIndex = (int)(chat.IsGlobal ? ChatType.Global : ChatType.Map);
        IsRandomChat = chat.IsRandom;

        var state = account.GetBehaviorState(BehaviorId);
        IsRunning = GameEngine.IsAutoChatRunning(account.Id);
        state.IsRunning = IsRunning;
        StatusText = !string.IsNullOrWhiteSpace(state.StatusText)
            ? state.StatusText
            : (IsRunning ? "Đang chạy auto chat..." : "Chưa kích hoạt");
        ChatSentCount = state.Counter;
    }

    protected override void SaveAccountCore(AccountItemViewModel account)
    {
        account.AutoSettings.SetBehaviorOptions(BehaviorId, BuildCurrentOptions());
    }

    partial void OnChatTextChanged(string value) => OnConfigChanged();
    partial void OnChatDelaySecondsChanged(int value) => OnConfigChanged();
    partial void OnSelectedChatTypeIndexChanged(int value) => OnConfigChanged();
    partial void OnIsRandomChatChanged(bool value) => OnConfigChanged();

    private void OnConfigChanged()
    {
        if (IsSwitchingAccount || ActiveAccount == null) return;

        PersistActiveAccountSettings();

        if (IsRunning)
        {
            var options = BuildCurrentOptions();
            var chatType = options.IsGlobal ? ChatType.Global : ChatType.Map;
            GameEngine.UpdateAutoChatConfig(ActiveAccount.Id, options.Messages, options.Delay, chatType, options.IsRandom);
        }
    }

    [RelayCommand]
    private void SetChatDelay(object? parameter)
    {
        if (parameter is int sec)
        {
            ChatDelaySeconds = Math.Max(1, sec);
        }
        else if (parameter != null && int.TryParse(parameter.ToString(), out int parsed))
        {
            ChatDelaySeconds = Math.Max(1, parsed);
        }
    }

    [RelayCommand]
    private void ToggleAutoChat()
    {
        if (IsRunning)
        {
            StopAutoChat();
        }
        else
        {
            StartAutoChat();
        }
    }

    [RelayCommand]
    private async Task SendChatOnce()
    {
        if (string.IsNullOrWhiteSpace(ChatText))
        {
            StatusText = "Nội dung chat trống!";
            return;
        }

        var lines = ChatText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        if (lines.Length == 0) return;

        string msg = lines[0];
        var targets = GetTargetAccountIds();
        var chatType = (ChatType)SelectedChatTypeIndex;

        if (targets.Count == 0)
        {
            StatusText = "Không có tài khoản nào trong game để gửi!";
            return;
        }

        int count = 0;
        foreach (var id in targets)
        {
            if (await GameEngine.SendChatAsync(id, msg, chatType)) count++;
        }

        StatusText = count > 0 ? $"Đã gửi 1 tin chat tới {count} tài khoản lúc {DateTime.Now:HH:mm:ss}" : "Không thể gửi tin nhắn chat!";
    }

    private AutoChatOptions BuildCurrentOptions(string? fallbackChatText = null)
    {
        var rawText = !string.IsNullOrWhiteSpace(ChatText) ? ChatText : (fallbackChatText ?? string.Empty);
        var lines = rawText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        return new AutoChatOptions
        {
            Messages = lines,
            Delay = TimeSpan.FromSeconds(Math.Max(1, ChatDelaySeconds <= 0 ? 5 : ChatDelaySeconds)),
            IsGlobal = (ChatType)SelectedChatTypeIndex == ChatType.Global,
            IsRandom = IsRandomChat
        };
    }

    private void StartAutoChat()
    {
        PersistActiveAccountSettings();

        if (string.IsNullOrWhiteSpace(ChatText))
        {
            StatusText = "Vui lòng nhập nội dung chat trước!";
            return;
        }

        var currentOptions = BuildCurrentOptions();
        if (currentOptions.Messages.Count == 0)
        {
            StatusText = "Vui lòng nhập nội dung chat trước!";
            return;
        }

        var targets = GetTargetAccountIds();
        if (targets.Count == 0)
        {
            StatusText = "Chưa chọn tài khoản đang chạy nào!";
            return;
        }

        ChatSentCount = 0;
        IsRunning = true;
        StatusText = targets.Count > 1 ? $"Đang chạy auto chat ({targets.Count} tài khoản)..." : "Đang chạy auto chat...";

        foreach (var id in targets)
        {
            var targetAcc = ResolveAccount(id);
            if (targetAcc != null && targetAcc.Id != ActiveAccount?.Id)
            {
                var savedChat = targetAcc.AutoSettings.GetBehaviorOptionsOrDefault<AutoChatOptions>(BehaviorId);
                var accOptions = savedChat.Messages is { Count: > 0 }
                    ? savedChat
                    : savedChat with { Messages = currentOptions.Messages };
                var state = targetAcc.GetBehaviorState(BehaviorId);
                state.IsRunning = true;
                state.StatusText = "Đang chạy auto chat...";
                GameEngine.StartAutoChat(id, accOptions);
            }
            else
            {
                if (targetAcc != null)
                {
                    var state = targetAcc.GetBehaviorState(BehaviorId);
                    state.IsRunning = true;
                    state.StatusText = "Đang chạy auto chat...";
                }
                GameEngine.StartAutoChat(id, currentOptions);
            }
        }
    }

    private void StopAutoChat()
    {
        var targets = GetTargetAccountIds();
        foreach (var id in targets)
        {
            GameEngine.StopAutoChat(id);
            var targetAcc = ResolveAccount(id);
            if (targetAcc != null)
            {
                var state = targetAcc.GetBehaviorState(BehaviorId);
                state.IsRunning = false;
                state.StatusText = "Đã dừng auto chat.";
            }
        }
        IsRunning = false;
        StatusText = $"Đã dừng (Đã gửi tổng cộng: {ChatSentCount} tin)";
    }

    protected override void OnBehaviorProgress(Guid accountId, string message)
    {
        RunOnUi(() =>
        {
            int count = ChatSentCount + 1;
            var target = ResolveAccount(accountId);
            if (target != null)
            {
                var state = target.GetBehaviorState(BehaviorId);
                state.Counter++;
                count = state.Counter;
                state.StatusText = $"Đang chạy... Đã gửi: {count} tin ({DateTime.Now:HH:mm:ss})";
            }

            if (ActiveAccount?.Id == accountId)
            {
                ChatSentCount = count;
                LastChatSentTime = DateTime.Now.ToString("HH:mm:ss");
                StatusText = $"Đang chạy... Đã gửi: {count} tin ({LastChatSentTime})";
            }
        });
    }
}
