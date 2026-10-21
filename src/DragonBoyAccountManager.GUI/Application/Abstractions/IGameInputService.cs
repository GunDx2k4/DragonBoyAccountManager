using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DragonBoyAccountManager.GUI.Application.Abstractions;

public interface IGameInputService
{
    void SetTargetResolver(Func<IEnumerable<Guid>> resolver);
    void StartHoldingKey(GameKey key);
    void StopHoldingKey(GameKey key);
    Task SendKeyPulseAsync(GameKey key, int holdMs = 120);
    Task SendRawKeyPulseAsync(int keyCode, int holdMs = 120);
    Task ActionOkAsync();
    Task ActionFocusAsync();
    Task ActionPickItemAsync();
    Task ActionUsePeanAsync();
    Task ActionShowPanelAsync();
    Task ActionHidePanelAsync();
    Task ActionYesAsync();
    Task ActionChatAsync();
    Task ActionBackAsync();
    void UseCapsule();
    void UseTdlt();
    void UseSkill(int slotIndex);
    void PreviousZone();
    void NextZone();
}
