using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DragonBoyAccountManager.GUI.ViewModels;

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
    public async Task MoveUpAsync() => await SendKeyPulseAsync(GameKey.Up);

    [RelayCommand]
    public async Task MoveDownAsync() => await SendKeyPulseAsync(GameKey.Down);

    [RelayCommand]
    public async Task MoveLeftAsync() => await SendKeyPulseAsync(GameKey.Left);

    [RelayCommand]
    public async Task MoveRightAsync() => await SendKeyPulseAsync(GameKey.Right);

    [RelayCommand]
    public async Task ActionOkAsync() => await SendKeyPulseAsync(GameKey.Ok);

    [RelayCommand]
    public async Task ActionFocusAsync() => await SendKeyPulseAsync(GameKey.Focus);

    [RelayCommand]
    public async Task ActionPickItemAsync() => await SendKeyPulseAsync(GameKey.PickItem);

    [RelayCommand]
    public async Task ActionUsePeanAsync() => await SendKeyPulseAsync(GameKey.UsePean);

    [RelayCommand]
    public async Task ActionShowPanelAsync() => await SendKeyPulseAsync(GameKey.ShowPanel);

    [RelayCommand]
    public async Task ActionHidePanelAsync() => await SendKeyPulseAsync(GameKey.HidePanel);

    [RelayCommand]
    public async Task ActionYesAsync() => await SendKeyPulseAsync(GameKey.Yes);

    [RelayCommand]
    public async Task ActionChatAsync() => await SendKeyPulseAsync(GameKey.Chat);

    [RelayCommand]
    public async Task ActionBackAsync() => await SendKeyPulseAsync(GameKey.Back);

    [RelayCommand]
    public async Task UseCapsuleAsync()
    {
        var targets = ResolveKeyTargets();
        foreach (var id in targets)
        {
            await _gameEngine.UseCapsuleAsync(id);
        }
    }

    [RelayCommand]
    public async Task UseTdltAsync()
    {
        var targets = ResolveKeyTargets();
        foreach (var id in targets)
        {
            await _gameEngine.UseTdltAsync(id);
        }
    }

    [RelayCommand]
    public async Task SelectSkillAsync(object? parameter)
    {
        int slotIndex = 0;
        if (parameter is int idx)
        {
            slotIndex = idx;
        }
        else if (parameter is string str && int.TryParse(str, out int parsed))
        {
            slotIndex = parsed;
        }
        else if (parameter is SkillButtonItem item)
        {
            slotIndex = item.SlotIndex;
        }

        var targets = ResolveKeyTargets();
        foreach (var id in targets)
        {
            await _gameEngine.SelectSkillAsync(id, slotIndex);
        }

        for (int i = 0; i < Skills.Count; i++)
        {
            Skills[i].IsSelected = (i == slotIndex);
        }
    }

    [RelayCommand]
    public void UseSkill(object? parameter)
    {
        int slotIndex = 0;
        SkillButtonItem? targetItem = null;
        if (parameter is int idx)
        {
            slotIndex = idx;
            if (slotIndex >= 0 && slotIndex < Skills.Count) targetItem = Skills[slotIndex];
        }
        else if (parameter is string str && int.TryParse(str, out int parsed))
        {
            slotIndex = parsed;
            if (slotIndex >= 0 && slotIndex < Skills.Count) targetItem = Skills[slotIndex];
        }
        else if (parameter is SkillButtonItem item)
        {
            slotIndex = item.SlotIndex;
            targetItem = item;
        }

        var targets = ResolveKeyTargets();
        foreach (var id in targets)
        {
            _ = _gameEngine.SelectSkillAsync(id, slotIndex);
        }

        if (targetItem != null)
        {
            targetItem.LastTimeUse = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            targetItem.UpdateCooldown(targetItem.LastTimeUse);
        }
    }

    [RelayCommand]
    public async Task AutoAssignSkillsAsync()
    {
        var targets = ResolveKeyTargets();
        foreach (var id in targets)
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
        var accountId = _editingAccount?.Id;
        if (accountId == null)
        {
            accountId = _selectionService?.GetRunningTargetAccountIds().FirstOrDefault() ?? _keyTargetResolver?.Invoke().FirstOrDefault();
        }
        if (accountId == null) return;

        var skills = _gameEngine.GetSkills(accountId.Value);
        var skillMap = skills.ToDictionary(s => s.Slot, s => s);
        for (int i = 0; i < Skills.Count; i++)
        {
            if (skillMap.TryGetValue(i, out var sInfo))
            {
                string expectedName = !string.IsNullOrWhiteSpace(sInfo.Name)
                    ? sInfo.Name
                    : (i == 9 ? "Skill 0" : $"Skill {i + 1}");

                if (Skills[i].Name != expectedName)
                {
                    Skills[i].Name = expectedName;
                }
                if (Skills[i].IsSelected != sInfo.IsSelected)
                {
                    Skills[i].IsSelected = sInfo.IsSelected;
                }
                Skills[i].Point = sInfo.Point;
                Skills[i].CoolDown = sInfo.CoolDown;
                Skills[i].ManaUse = sInfo.ManaUse;
                Skills[i].LastTimeUse = sInfo.LastTimeUse;
                if (sInfo.IconId >= 0 && Skills[i].IconId != sInfo.IconId)
                {
                    Skills[i].IconId = sInfo.IconId;
                    Skills[i].Icon = _gameImageService.GetSmallImage(sInfo.IconId);
                }
            }
            else
            {
                string defaultName = i == 9 ? "Skill 0" : $"Skill {i + 1}";
                if (Skills[i].Name != defaultName)
                {
                    Skills[i].Name = defaultName;
                }
                if (Skills[i].IsSelected)
                {
                    Skills[i].IsSelected = false;
                }
                Skills[i].Point = 0;
                Skills[i].CoolDown = 0;
                Skills[i].ManaUse = 0;
                Skills[i].LastTimeUse = 0;
                Skills[i].IconId = -1;
                Skills[i].Icon = null;
            }
        }
        UpdateCooldowns();
    }

    [RelayCommand]
    public void NextZone()
    {
        int next = (CurrentZoneId ?? 0) + 1;
        ExecuteChangeZone(next);
    }

    [RelayCommand]
    public void PreviousZone()
    {
        int prev = (CurrentZoneId ?? 0) - 1;
        if (prev < 0) prev = 0;
        ExecuteChangeZone(prev);
    }

    public void ExecuteChangeZone(int targetZone)
    {
        if (targetZone < 0) return;
        var targets = ResolveKeyTargets();
        foreach (var id in targets)
        {
            _gameEngine.ChangeZone(id, targetZone);
        }
    }

    public void RequestZoneList()
    {
        var accountId = _editingAccount?.Id;
        if (accountId == null)
        {
            accountId = _selectionService?.GetRunningTargetAccountIds().FirstOrDefault() ?? _keyTargetResolver?.Invoke().FirstOrDefault();
        }
        if (accountId == null) return;

        _gameEngine.RequestZoneList(accountId.Value);
        var snapshot = _gameEngine.GetWorldSnapshot(accountId.Value);
        if (snapshot != null && snapshot.Zones.Count > 0)
        {
            UpdateZones(snapshot.Zones, snapshot.ZoneId);
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(350);
            var freshSnapshot = _gameEngine.GetWorldSnapshot(accountId.Value);
            if (freshSnapshot != null && freshSnapshot.Zones.Count > 0)
            {
                var dispatcher = App.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() => UpdateZones(freshSnapshot.Zones, freshSnapshot.ZoneId));
                }
            }
        });
    }

    public void UpdateZones(IReadOnlyList<WorldZoneInfo> newZones, int currentZoneId)
    {
        _isRefreshingZones = true;
        try
        {
            bool needRebuild = AvailableZones.Count != newZones.Count;
            if (!needRebuild)
            {
                for (int i = 0; i < newZones.Count; i++)
                {
                    if (AvailableZones[i].ZoneId != newZones[i].ZoneId)
                    {
                        needRebuild = true;
                        break;
                    }
                }
            }

            if (needRebuild)
            {
                AvailableZones.Clear();
                foreach (var z in newZones)
                {
                    AvailableZones.Add(z);
                }
            }
            else
            {
                for (int i = 0; i < newZones.Count; i++)
                {
                    if (AvailableZones[i] != newZones[i])
                    {
                        AvailableZones[i] = newZones[i];
                    }
                }
            }

            CurrentZoneId = currentZoneId;
            if (SelectedZone?.ZoneId != currentZoneId)
            {
                _selectedZone = AvailableZones.FirstOrDefault(z => z.ZoneId == currentZoneId);
                OnPropertyChanged(nameof(SelectedZone));
            }
        }
        finally
        {
            _isRefreshingZones = false;
        }
    }

    [RelayCommand]
    private void OpenCheatsheet()
    {
        _dialogService.ShowHotkeyCheatsheet(null);
    }

    public void StartHoldingKey(GameKey key)
    {
        var targets = ResolveKeyTargets();
        foreach (var id in targets)
        {
            _gameEngine.SendGameKeyDown(id, key);
        }
    }

    public void StopHoldingKey(GameKey key)
    {
        var targets = ResolveKeyTargets();
        foreach (var id in targets)
        {
            _gameEngine.SendGameKeyUp(id, key);
        }
    }

    public async Task SendKeyPulseAsync(GameKey key, int holdMs = 120)
    {
        var targets = ResolveKeyTargets().ToList();
        if (targets.Count == 0) return;

        var tasks = targets.Select(id => _gameEngine.SendGameKeyPressAsync(id, key, holdMs));
        await Task.WhenAll(tasks);
    }

    public void StartHoldingRawKey(int keyCode)
    {
        var targets = ResolveKeyTargets();
        foreach (var id in targets)
        {
            _gameEngine.SendGameKeyDown(id, keyCode);
        }
    }

    public void StopHoldingRawKey(int keyCode)
    {
        var targets = ResolveKeyTargets();
        foreach (var id in targets)
        {
            _gameEngine.SendGameKeyUp(id, keyCode);
        }
    }

    public async Task SendRawKeyPulseAsync(int keyCode, int holdMs = 120)
    {
        var targets = ResolveKeyTargets().ToList();
        if (targets.Count == 0 || keyCode == 0) return;

        var tasks = targets.Select(id => _gameEngine.SendGameKeyPressAsync(id, keyCode, holdMs));
        await Task.WhenAll(tasks);
    }

    private IEnumerable<Guid> ResolveKeyTargets()
    {
        if (_selectionService != null)
        {
            var running = _selectionService.GetRunningTargetAccountIds();
            if (running.Count > 0) return running;
        }

        if (_keyTargetResolver != null)
        {
            return _keyTargetResolver();
        }

        if (_targetResolver != null)
        {
            return _targetResolver();
        }

        if (_editingAccount != null)
        {
            return [_editingAccount.Id];
        }

        return [];
    }

    private IEnumerable<Guid> ResolveSpeedTargets()
    {
        if (_selectionService != null)
        {
            var running = _selectionService.GetRunningSpeedTargetAccountIds(IsApplySpeedToAll);
            if (running.Count > 0) return running;
        }

        if (_speedTargetResolver != null)
        {
            return _speedTargetResolver();
        }

        if (_targetResolver != null)
        {
            return _targetResolver();
        }

        if (_editingAccount != null)
        {
            return [_editingAccount.Id];
        }

        return [];
    }
}
