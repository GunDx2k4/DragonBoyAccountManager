using System.Threading.Tasks;
using DragonBoyAccountManager.Core.Common;

namespace DragonBoyAccountManager.GUI.Services.Input;

public interface IGameInputService
{
    void StartHoldingKey(GameKey key);
    void StopHoldingKey(GameKey key);
    void StartHoldingRawKey(int keyCode);
    void StopHoldingRawKey(int keyCode);
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
    Task UseCapsuleAsync();
    void UseTdlt();
    Task UseTdltAsync();
    void UseSkill(int slotIndex);
    Task UseSkillAsync(int slotIndex);
    void PreviousZone();
    void NextZone();
}
