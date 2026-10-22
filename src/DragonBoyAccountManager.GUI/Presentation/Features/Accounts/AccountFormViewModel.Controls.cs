using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.GUI.Presentation.DesignSystem.Assists;
using DragonBoyAccountManager.GUI.Presentation.Features.Auto.Items;
using DragonBoyAccountManager.GUI.Services.Engine;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Accounts;

public sealed partial class AccountFormViewModel
{
    [RelayCommand]
    private void SetGameSpeed(string speedStr)
    {
        if (float.TryParse(speedStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float speed))
        {
            GameSpeed = speed;
        }
    }

    [RelayCommand]
    public Task MoveUpAsync() => SendKeyPulseAsync(GameKey.Up);

    [RelayCommand]
    public Task MoveDownAsync() => SendKeyPulseAsync(GameKey.Down);

    [RelayCommand]
    public Task MoveLeftAsync() => SendKeyPulseAsync(GameKey.Left);

    [RelayCommand]
    public Task MoveRightAsync() => SendKeyPulseAsync(GameKey.Right);

    [RelayCommand]
    public Task ActionOkAsync() => _gameInput.ActionOkAsync();

    [RelayCommand]
    public Task ActionFocusAsync() => _gameInput.ActionFocusAsync();

    [RelayCommand]
    public Task ActionPickItemAsync() => _gameInput.ActionPickItemAsync();

    [RelayCommand]
    public Task ActionUsePeanAsync() => _gameInput.ActionUsePeanAsync();

    [RelayCommand]
    public Task ActionShowPanelAsync() => _gameInput.ActionShowPanelAsync();

    [RelayCommand]
    public Task ActionHidePanelAsync() => _gameInput.ActionHidePanelAsync();

    [RelayCommand]
    public Task ActionYesAsync() => _gameInput.ActionYesAsync();

    [RelayCommand]
    public Task ActionChatAsync() => _gameInput.ActionChatAsync();

    [RelayCommand]
    public Task ActionBackAsync() => _gameInput.ActionBackAsync();

    [RelayCommand]
    public Task UseCapsuleAsync() => _gameInput.UseCapsuleAsync();

    [RelayCommand]
    public Task UseTdltAsync() => _gameInput.UseTdltAsync();

    private static int ParseSlotIndex(object? parameter, out SkillButtonItem? targetItem, IList<SkillButtonItem> skills)
    {
        targetItem = null;
        int slotIndex = 0;

        if (parameter is int idx)
        {
            slotIndex = idx;
            if (slotIndex >= 0 && slotIndex < skills.Count) targetItem = skills[slotIndex];
        }
        else if (parameter is string str && int.TryParse(str, out int parsed))
        {
            slotIndex = parsed;
            if (slotIndex >= 0 && slotIndex < skills.Count) targetItem = skills[slotIndex];
        }
        else if (parameter is SkillButtonItem item)
        {
            slotIndex = item.SlotIndex;
            targetItem = item;
        }

        return slotIndex;
    }

    [RelayCommand]
    public async Task SelectSkillAsync(object? parameter)
    {
        int slotIndex = ParseSlotIndex(parameter, out _, Skills);
        await _gameInput.UseSkillAsync(slotIndex);

        for (int i = 0; i < Skills.Count; i++)
        {
            Skills[i].IsSelected = (i == slotIndex);
        }
    }

    [RelayCommand]
    public void UseSkill(object? parameter)
    {
        int slotIndex = ParseSlotIndex(parameter, out var targetItem, Skills);
        _gameInput.UseSkill(slotIndex);

        if (targetItem != null)
        {
            targetItem.LastTimeUse = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            targetItem.UpdateCooldown(targetItem.LastTimeUse);
        }
    }

    [RelayCommand]
    public async Task AutoAssignSkillsAsync()
    {
        foreach (var id in ResolveKeyTargets())
        {
            await _gameEngine.AutoAssignSkillsAsync(id);
        }
        RefreshSkills();
    }

    private void UpdateCooldowns()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var item in Skills)
        {
            item.UpdateCooldown(now);
        }
    }

    public void RefreshSkills()
    {
        var accountId = _editingAccount?.Id ?? _selectionService?.GetRunningTargetAccountIds().FirstOrDefault();
        if (accountId == null) return;

        var skillMap = _gameEngine.GetSkills(accountId.Value).ToDictionary(s => s.Slot);
        for (int i = 0; i < Skills.Count; i++)
        {
            skillMap.TryGetValue(i, out var info);
            Skills[i].SyncFrom(info, _gameImageService, syncSelection: true);
        }
        UpdateCooldowns();
    }

    [RelayCommand]
    public void NextZone()
    {
        ExecuteChangeZone((CurrentZoneId ?? 0) + 1);
    }

    [RelayCommand]
    public void PreviousZone()
    {
        ExecuteChangeZone(Math.Max(0, (CurrentZoneId ?? 0) - 1));
    }

    public void ExecuteChangeZone(int targetZone)
    {
        if (targetZone < 0) return;
        foreach (var id in ResolveKeyTargets())
        {
            _gameEngine.ChangeZone(id, targetZone);
        }
    }

    public void RequestZoneList()
    {
        var accountId = _editingAccount?.Id ?? _selectionService?.GetRunningTargetAccountIds().FirstOrDefault();
        if (accountId == null) return;

        _gameEngine.RequestZoneList(accountId.Value);
        var snapshot = _gameEngine.GetWorldSnapshot(accountId.Value);
        if (snapshot != null && snapshot.Zones.Count > 0)
        {
            UpdateZones(snapshot.Zones, snapshot.ZoneId);
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(350, CancellationToken.None);
            var freshSnapshot = _gameEngine.GetWorldSnapshot(accountId.Value);
            if (freshSnapshot != null && freshSnapshot.Zones.Count > 0)
            {
                RunOnUi(() => UpdateZones(freshSnapshot.Zones, freshSnapshot.ZoneId));
            }
        });
    }

    public void UpdateZones(IReadOnlyList<WorldZoneInfo> newZones, int currentZoneId)
    {
        CurrentZoneId = currentZoneId;
        AvailableZones.SyncZones(newZones, currentZoneId, ref _selectedZone, ref _isRefreshingZones, () => OnPropertyChanged(nameof(SelectedZone)));
    }

    [RelayCommand]
    private void OpenCheatsheet() => _dialogService.ShowHotkeyCheatsheet(null);

    public void StartHoldingKey(GameKey key) => _gameInput.StartHoldingKey(key);
    public void StopHoldingKey(GameKey key) => _gameInput.StopHoldingKey(key);
    public Task SendKeyPulseAsync(GameKey key, int holdMs = 120) => _gameInput.SendKeyPulseAsync(key, holdMs);
    public void StartHoldingRawKey(int keyCode) => _gameInput.StartHoldingRawKey(keyCode);
    public void StopHoldingRawKey(int keyCode) => _gameInput.StopHoldingRawKey(keyCode);
    public Task SendRawKeyPulseAsync(int keyCode, int holdMs = 120) => _gameInput.SendRawKeyPulseAsync(keyCode, holdMs);

    private IEnumerable<Guid> ResolveKeyTargets()
    {
        if (_selectionService != null)
        {
            var running = _selectionService.GetRunningTargetAccountIds();
            if (running.Count > 0) return running;
        }

        return _editingAccount != null ? [_editingAccount.Id] : [];
    }

    private IEnumerable<Guid> ResolveSpeedTargets()
    {
        if (_selectionService != null)
        {
            var running = _selectionService.GetRunningSpeedTargetAccountIds(IsApplySpeedToAll);
            if (running.Count > 0) return running;
        }

        return _editingAccount != null ? [_editingAccount.Id] : [];
    }
}
