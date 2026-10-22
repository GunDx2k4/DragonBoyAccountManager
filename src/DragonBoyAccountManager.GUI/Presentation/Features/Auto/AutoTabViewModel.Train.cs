using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto;

public sealed partial class AutoTabViewModel
{
    [RelayCommand]
    public void ToggleAutoTrain()
    {
        if (IsAutoTrainRunning)
        {
            StopAutoTrain();
        }
        else
        {
            StartAutoTrain();
        }
    }

    [RelayCommand]
    public void ClearMobSearch()
    {
        MobSearchText = string.Empty;
    }

    [RelayCommand]
    public void SelectAllMobs()
    {
        if (!string.IsNullOrWhiteSpace(MobSearchText))
        {
            foreach (var obj in FilteredMobTypes)
            {
                if (obj is MobFilterItem item)
                {
                    item.IsSelected = true;
                }
            }
        }
        else
        {
            foreach (var item in AvailableMobTypes)
            {
                item.IsSelected = true;
            }
        }
        OnAutoTrainOptionChanged();
    }

    [RelayCommand]
    public void DeselectAllMobs()
    {
        if (!string.IsNullOrWhiteSpace(MobSearchText))
        {
            foreach (var obj in FilteredMobTypes)
            {
                if (obj is MobFilterItem item)
                {
                    item.IsSelected = false;
                }
            }
        }
        else
        {
            foreach (var item in AvailableMobTypes)
            {
                item.IsSelected = false;
            }
        }
        OnAutoTrainOptionChanged();
    }

    [RelayCommand]
    public void AddCustomMobId()
    {
        if (string.IsNullOrWhiteSpace(CustomMobIdInput)) return;

        var tokens = CustomMobIdInput.Split([',', ';', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var snapshot = _gameEngine.GetWorldSnapshot(ActiveAccount?.Id ?? Guid.Empty);

        foreach (var token in tokens)
        {
            string clean = token.Trim().TrimStart('#');
            if (int.TryParse(clean, out int id))
            {
                if (MobTargetModeIndex == 0)
                {
                    var existing = AvailableMobTypes.FirstOrDefault(m => !m.MobId.HasValue && m.TemplateId == id);
                    if (existing != null)
                    {
                        existing.IsSelected = true;
                    }
                    else
                    {
                        var match = snapshot?.Mobs.FirstOrDefault(m => m.TemplateId == id);
                        string mobName = match != null ? match.Name : $"Mục tiêu #{id}";
                        long hp = match?.Hp ?? 0;
                        long maxHp = match?.MaxHp ?? 0;
                        bool isFlying = match?.IsFlying ?? false;
                        bool isSuperMob = match?.IsSuperMob ?? false;

                        var customItem = new MobFilterItem(
                            templateId: id,
                            name: mobName,
                            hp: hp,
                            maxHp: maxHp,
                            isFlying: isFlying,
                            isSuperMob: isSuperMob,
                            count: 1,
                            isSelected: true,
                            isCustom: true
                        );
                        customItem.PropertyChanged += OnMobItemPropertyChanged;
                        AvailableMobTypes.Add(customItem);
                    }
                }
                else
                {
                    var existing = AvailableMobTypes.FirstOrDefault(m => m.MobId == id);
                    if (existing != null)
                    {
                        existing.IsSelected = true;
                    }
                    else
                    {
                        var match = snapshot?.Mobs.FirstOrDefault(m => m.Id == id);
                        string mobName = match != null ? match.Name : $"Mục tiêu #{id}";
                        long hp = match?.Hp ?? 0;
                        long maxHp = match?.MaxHp ?? 0;
                        bool isFlying = match?.IsFlying ?? false;
                        bool isSuperMob = match?.IsSuperMob ?? false;
                        int templateId = match?.TemplateId ?? -1;

                        var customItem = new MobFilterItem(
                            templateId: templateId,
                            name: mobName,
                            hp: hp,
                            maxHp: maxHp,
                            isFlying: isFlying,
                            isSuperMob: isSuperMob,
                            count: 1,
                            isSelected: true,
                            isCustom: true,
                            mobId: id
                        );
                        customItem.PropertyChanged += OnMobItemPropertyChanged;
                        AvailableMobTypes.Add(customItem);
                    }
                }
            }
        }

        CustomMobIdInput = string.Empty;
        FilteredMobTypes.Refresh();
        OnAutoTrainOptionChanged();
    }

    [RelayCommand]
    public void RemoveCustomMob(MobFilterItem? item)
    {
        if (item != null && item.IsCustom)
        {
            item.PropertyChanged -= OnMobItemPropertyChanged;
            AvailableMobTypes.Remove(item);
            FilteredMobTypes.Refresh();
            OnAutoTrainOptionChanged();
        }
    }

    [RelayCommand]
    public void SelectAllSkills()
    {
        foreach (var item in AutoTrainSkills)
        {
            item.IsSelected = true;
        }
        OnAutoTrainOptionChanged();
    }

    [RelayCommand]
    public void DeselectAllSkills()
    {
        foreach (var item in AutoTrainSkills)
        {
            item.IsSelected = false;
        }
        OnAutoTrainOptionChanged();
    }

    private AutoTrainOptions BuildCurrentUIOptions()
    {
        var selectedSlots = AutoTrainSkills.Where(s => s.IsSelected).Select(s => s.SlotIndex).ToList();
        if (selectedSlots.Count == 0)
        {
            selectedSlots = [0];
        }

        var selectedMobTemplates = new List<int>();
        var selectedMobIds = new List<int>();

        if (MobTargetModeIndex == 0)
        {
            selectedMobTemplates = AvailableMobTypes
                .Where(m => m.IsSelected && !m.MobId.HasValue && m.TemplateId >= 0)
                .Select(m => m.TemplateId)
                .Distinct()
                .ToList();
        }
        else
        {
            selectedMobIds = AvailableMobTypes
                .Where(m => m.IsSelected && m.MobId.HasValue)
                .Select(m => m.MobId!.Value)
                .Distinct()
                .ToList();
        }

        int minHp = ActiveAccount?.AutoSettings.MinHpPercent ?? 20;
        int minMp = ActiveAccount?.AutoSettings.MinMpPercent ?? 20;

        return new AutoTrainOptions
        {
            TargetMode = (MobTargetMode)MobTargetModeIndex,
            SelectedSkillSlots = selectedSlots,
            TargetMobTemplateIds = selectedMobTemplates,
            TargetMobIds = selectedMobIds,
            IgnoreBosses = IgnoreBosses,
            AvoidSuperMobs = AvoidSuperMobs,
            AutoPickItems = AutoPickItems,
            AutoUsePean = AutoUsePean,
            UseTdlt = UseTdlt,
            MinHpPercent = minHp,
            MinMpPercent = minMp
        };
    }

    private void StartAutoTrain()
    {
        SaveCurrentAccountSettings();

        var targets = GetTargetAccountIds(AutoTrainSyncAll);
        if (targets.Count == 0)
        {
            StatusText = "Chưa chọn tài khoản đang chạy nào!";
            AutoTrainStatusText = "Chưa chọn tài khoản!";
            return;
        }

        IsAutoTrainRunning = true;
        AutoTrainStatusText = targets.Count > 1 ? $"Đang khởi động ({targets.Count} tài khoản)..." : "Đang khởi động...";

        foreach (var id in targets)
        {
            var targetAcc = ResolveAccount(id);
            var options = targetAcc != null && targetAcc.Id != ActiveAccount?.Id
                ? targetAcc.AutoSettings.ToAutoTrainOptions()
                : BuildCurrentUIOptions();

            if (targetAcc != null)
            {
                targetAcc.IsAutoTrainRunning = true;
                targetAcc.AutoTrainStatusText = "Đang khởi động...";
            }

            _gameEngine.StartAutoTrain(id, options);
        }
    }

    private void StopAutoTrain()
    {
        var targets = GetTargetAccountIds(AutoTrainSyncAll);
        foreach (var id in targets)
        {
            _gameEngine.StopAutoTrain(id);
            var targetAcc = ResolveAccount(id);
            if (targetAcc != null)
            {
                targetAcc.IsAutoTrainRunning = false;
                targetAcc.AutoTrainStatusText = "Đã dừng tự động luyện tập.";
            }
        }
        IsAutoTrainRunning = false;
        AutoTrainStatusText = "Đã dừng tự động luyện tập.";
    }

    private void HandleAutoTrainStatusChanged(Guid accountId, string message)
    {
        RunOnUi(() =>
        {
            var target = ResolveAccount(accountId);
            if (target != null)
            {
                target.AutoTrainStatusText = message;
            }

            if (ActiveAccount?.Id == accountId)
            {
                AutoTrainStatusText = message;
            }
        });
    }

    private void HandleAutoTrainStateChanged(Guid accountId, bool isRunning)
    {
        RunOnUi(() =>
        {
            var target = ResolveAccount(accountId);
            if (target != null)
            {
                target.IsAutoTrainRunning = isRunning;
            }

            if (ActiveAccount?.Id == accountId)
            {
                IsAutoTrainRunning = isRunning;
            }
        });
    }

    partial void OnAutoTrainSyncAllChanged(bool value)
    {
        if (value)
        {
            var targets = GetTargetAccountIds();
            IsAutoTrainRunning = targets.Any(id => _gameEngine.IsAutoTrainRunning(id));
        }
        else
        {
            IsAutoTrainRunning = ActiveAccount != null && _gameEngine.IsAutoTrainRunning(ActiveAccount.Id);
            AutoTrainStatusText = ActiveAccount?.AutoTrainStatusText ?? (IsAutoTrainRunning ? "Đang chạy..." : "Chưa kích hoạt");
        }
    }
}
