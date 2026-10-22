using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto;

public sealed partial class AutoTabViewModel
{

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
        if (IsAutoChatRunning)
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
        var targets = GetTargetAccountIds(AutoChatSyncAll);
        var chatType = (ChatType)SelectedChatTypeIndex;

        if (targets.Count == 0)
        {
            StatusText = "Không có tài khoản nào trong game để gửi!";
            return;
        }

        int count = 0;
        foreach (var id in targets)
        {
            if (await _gameEngine.SendChatAsync(id, msg, chatType)) count++;
        }

        StatusText = count > 0 ? $"Đã gửi 1 tin chat tới {count} tài khoản lúc {DateTime.Now:HH:mm:ss}" : "Không thể gửi tin nhắn chat!";
    }

    private void StartAutoChat()
    {
        SaveCurrentAccountSettings();

        if (string.IsNullOrWhiteSpace(ChatText))
        {
            StatusText = "Vui lòng nhập nội dung chat trước!";
            return;
        }

        var lines = ChatText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        if (lines.Length == 0)
        {
            StatusText = "Vui lòng nhập nội dung chat trước!";
            return;
        }

        var targets = GetTargetAccountIds(AutoChatSyncAll);
        if (targets.Count == 0)
        {
            StatusText = "Chưa chọn tài khoản đang chạy nào!";
            return;
        }

        var chatType = (ChatType)SelectedChatTypeIndex;
        var interval = TimeSpan.FromSeconds(Math.Max(1, ChatDelaySeconds));

        ChatSentCount = 0;
        IsAutoChatRunning = true;
        AutoChatStatusText = targets.Count > 1 ? $"Đang chạy auto chat ({targets.Count} tài khoản)..." : "Đang chạy auto chat...";

        foreach (var id in targets)
        {
            var targetAcc = ResolveAccount(id);
            if (targetAcc != null && targetAcc.Id != ActiveAccount?.Id)
            {
                var accOptions = targetAcc.AutoSettings.ToAutoChatOptions(ChatText);
                targetAcc.IsAutoChatRunning = true;
                targetAcc.AutoChatStatusText = "Đang chạy auto chat...";
                _gameEngine.StartAutoChat(id, accOptions);
            }
            else
            {
                if (targetAcc != null)
                {
                    targetAcc.IsAutoChatRunning = true;
                    targetAcc.AutoChatStatusText = "Đang chạy auto chat...";
                }
                _gameEngine.StartAutoChat(id, lines, interval, chatType, IsRandomChat);
            }
        }
    }

    private void StopAutoChat()
    {
        var targets = GetTargetAccountIds(AutoChatSyncAll);
        foreach (var id in targets)
        {
            _gameEngine.StopAutoChat(id);
            var targetAcc = ResolveAccount(id);
            if (targetAcc != null)
            {
                targetAcc.IsAutoChatRunning = false;
                targetAcc.AutoChatStatusText = "Đã dừng auto chat.";
            }
        }
        IsAutoChatRunning = false;
        AutoChatStatusText = $"Đã dừng (Đã gửi tổng cộng: {ChatSentCount} tin)";
    }

    private void HandleChatSent(Guid accountId, string message)
    {
        RunOnUi(() =>
        {
            int count = ChatSentCount + 1;
            var target = ResolveAccount(accountId);
            if (target != null)
            {
                target.ChatSentCount++;
                count = target.ChatSentCount;
                target.AutoChatStatusText = $"Đang chạy... Đã gửi: {count} tin ({DateTime.Now:HH:mm:ss})";
            }

            if (ActiveAccount?.Id == accountId)
            {
                ChatSentCount = count;
                LastChatSentTime = DateTime.Now.ToString("HH:mm:ss");
                AutoChatStatusText = $"Đang chạy... Đã gửi: {count} tin ({LastChatSentTime})";
            }
        });
    }

    private void HandleAutoChatStateChanged(Guid accountId, bool isRunning)
    {
        RunOnUi(() =>
        {
            var target = ResolveAccount(accountId);
            if (target != null)
            {
                target.IsAutoChatRunning = isRunning;
            }

            if (ActiveAccount?.Id == accountId)
            {
                IsAutoChatRunning = isRunning;
            }
        });
    }

    partial void OnAutoChatSyncAllChanged(bool value)
    {
        if (value)
        {
            var targets = GetTargetAccountIds();
            IsAutoChatRunning = targets.Any(id => _gameEngine.IsAutoChatRunning(id));
        }
        else
        {
            IsAutoChatRunning = ActiveAccount != null && _gameEngine.IsAutoChatRunning(ActiveAccount.Id);
            AutoChatStatusText = ActiveAccount?.AutoChatStatusText ?? (IsAutoChatRunning ? "Đang chạy..." : "Chưa kích hoạt");
        }
    }
}
