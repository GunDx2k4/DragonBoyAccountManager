using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DragonBoyAccountManager.GUI.Application.Abstractions;

namespace DragonBoyAccountManager.GUI.Infrastructure.Input;

public sealed class GameInputService(
    IGameEngineService gameEngine,
    IActiveSelectionService? selectionService = null) : IGameInputService
{
    private readonly IGameEngineService _gameEngine = gameEngine;
    private readonly IActiveSelectionService? _selectionService = selectionService;
    private Func<IEnumerable<Guid>>? _targetResolver;

    public void SetTargetResolver(Func<IEnumerable<Guid>> resolver)
    {
        _targetResolver = resolver;
    }

    private IEnumerable<Guid> ResolveTargets() =>
        _selectionService?.GetRunningTargetAccountIds() ?? _targetResolver?.Invoke() ?? [];

    public void StartHoldingKey(GameKey key)
    {
        foreach (var id in ResolveTargets())
        {
            _gameEngine.SendGameKeyDown(id, key);
        }
    }

    public void StopHoldingKey(GameKey key)
    {
        foreach (var id in ResolveTargets())
        {
            _gameEngine.SendGameKeyUp(id, key);
        }
    }

    public async Task SendKeyPulseAsync(GameKey key, int holdMs = 120)
    {
        var targets = ResolveTargets().ToList();
        if (targets.Count == 0) return;
        var tasks = targets.Select(id => _gameEngine.SendGameKeyPressAsync(id, key, holdMs));
        await Task.WhenAll(tasks);
    }

    public async Task SendRawKeyPulseAsync(int keyCode, int holdMs = 120)
    {
        var targets = ResolveTargets().ToList();
        if (targets.Count == 0 || keyCode == 0) return;
        var tasks = targets.Select(id => _gameEngine.SendGameKeyPressAsync(id, keyCode, holdMs));
        await Task.WhenAll(tasks);
    }

    public Task ActionOkAsync() => SendKeyPulseAsync(GameKey.Ok);
    public Task ActionFocusAsync() => SendKeyPulseAsync(GameKey.Focus);
    public Task ActionPickItemAsync() => SendKeyPulseAsync(GameKey.PickItem);
    public Task ActionUsePeanAsync() => SendKeyPulseAsync(GameKey.UsePean);
    public Task ActionShowPanelAsync() => SendKeyPulseAsync(GameKey.ShowPanel);
    public Task ActionHidePanelAsync() => SendKeyPulseAsync(GameKey.HidePanel);
    public Task ActionYesAsync() => SendKeyPulseAsync(GameKey.Yes);
    public Task ActionChatAsync() => SendKeyPulseAsync(GameKey.Chat);
    public Task ActionBackAsync() => SendKeyPulseAsync(GameKey.Back);

    public void UseCapsule() => _ = UseCapsuleAsync();

    public async Task UseCapsuleAsync()
    {
        foreach (var id in ResolveTargets())
        {
            await _gameEngine.UseCapsuleAsync(id);
        }
    }

    public void UseTdlt() => _ = UseTdltAsync();

    public async Task UseTdltAsync()
    {
        foreach (var id in ResolveTargets())
        {
            await _gameEngine.UseTdltAsync(id);
        }
    }

    public void UseSkill(int slotIndex) => _ = UseSkillAsync(slotIndex);

    public async Task UseSkillAsync(int slotIndex)
    {
        foreach (var id in ResolveTargets())
        {
            await _gameEngine.SelectSkillAsync(id, slotIndex);
        }
    }

    public void PreviousZone()
    {
        foreach (var id in ResolveTargets())
        {
            var snap = _gameEngine.GetWorldSnapshot(id);
            if (snap != null)
            {
                int prevZone = Math.Max(0, snap.ZoneId - 1);
                _gameEngine.ChangeZone(id, prevZone);
            }
        }
    }

    public void NextZone()
    {
        foreach (var id in ResolveTargets())
        {
            var snap = _gameEngine.GetWorldSnapshot(id);
            if (snap != null)
            {
                _gameEngine.ChangeZone(id, snap.ZoneId + 1);
            }
        }
    }
}
