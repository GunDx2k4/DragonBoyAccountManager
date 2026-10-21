using System.Windows;
using System.Windows.Input;

namespace DragonBoyAccountManager.GUI.Application.Abstractions;

public interface IHotkeyRoutingService
{
    bool HandlePreviewKeyDown(KeyEventArgs e, Window? ownerWindow = null);
    bool HandlePreviewKeyUp(KeyEventArgs e);
    void ReleaseAllHeldKeys();
}
