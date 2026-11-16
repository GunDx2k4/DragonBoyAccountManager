using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.GUI.Presentation.Features.Auto.Items;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto;

public sealed partial class AutoTrainViewModel
{
    private DispatcherTimer _cooldownTimer = null!;

    public ObservableCollection<SkillButtonItem> AutoTrainSkills { get; } = [];

    private void InitializeSkills()
    {
        for (int i = 0; i < 10; i++)
        {
            var skill = new SkillButtonItem(i, i == 9 ? "Skill 0" : $"Skill {i + 1}", i == 0);
            skill.PropertyChanged += OnSkillItemPropertyChanged;
            AutoTrainSkills.Add(skill);
        }

        _cooldownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _cooldownTimer.Tick += (_, _) => UpdateCooldowns();
    }

    [RelayCommand]
    public void SelectAllSkills()
    {
        RunBulkUpdate(() =>
        {
            for (int i = 0; i < AutoTrainSkills.Count; i++)
            {
                var item = AutoTrainSkills[i];
                item.IsSelected = item.TemplateId != 14;
            }
        });
        OnOptionChanged();
    }

    [RelayCommand]
    public void DeselectAllSkills()
    {
        RunBulkUpdate(() =>
        {
            for (int i = 0; i < AutoTrainSkills.Count; i++)
            {
                AutoTrainSkills[i].IsSelected = false;
            }
        });
        OnOptionChanged();
    }

    [RelayCommand]
    public async Task UseSkillSlotAsync(SkillButtonItem? item)
    {
        if (item == null || ActiveAccount == null || !ActiveAccount.IsRunning)
        {
            return;
        }

        await GameEngine.SelectSkillAsync(ActiveAccount.Id, item.SlotIndex);
        item.LastTimeUse = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        item.UpdateCooldown(item.LastTimeUse);
    }

    private void RefreshSkills()
    {
        if (ActiveAccount == null || !ActiveAccount.IsRunning)
        {
            return;
        }

        var skills = GameEngine.GetSkills(ActiveAccount.Id);
        if (skills.Count == 0)
        {
            for (int i = 0; i < AutoTrainSkills.Count; i++)
            {
                AutoTrainSkills[i].Reset(resetSelection: false);
            }
            UpdateCooldowns();
            return;
        }

        Span<bool> populatedSlots = stackalloc bool[AutoTrainSkills.Count];
        for (int i = 0; i < skills.Count; i++)
        {
            var skill = skills[i];
            if (skill.Slot >= 0 && skill.Slot < AutoTrainSkills.Count)
            {
                AutoTrainSkills[skill.Slot].SyncFrom(skill, _gameImageService, syncSelection: false);
                populatedSlots[skill.Slot] = true;
            }
        }

        for (int i = 0; i < AutoTrainSkills.Count; i++)
        {
            if (!populatedSlots[i])
            {
                AutoTrainSkills[i].Reset(resetSelection: false);
            }
        }

        UpdateCooldowns();
    }

    private void UpdateCooldowns()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (int i = 0; i < AutoTrainSkills.Count; i++)
        {
            AutoTrainSkills[i].UpdateCooldown(now);
        }
    }

    private void OnSkillItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SkillButtonItem.IsSelected))
        {
            OnOptionChanged();
        }
    }
}
