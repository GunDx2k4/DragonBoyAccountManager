using DragonBoyAccountManager.Core.Game.Context;

namespace DragonBoyAccountManager.Core.Game.Actions.Interrupters;

/// <summary>
/// Tự động đóng/bỏ qua các popup thông báo hoặc dialog chờ bất ngờ của game làm kẹt luồng action.
/// </summary>
public class DialogDismissInterrupter(int priority = 100) : IActionInterrupter
{
    public string Name => "Dialog Dismiss Interrupter";
    public int Priority => priority;

    public bool ShouldInterrupt(IGameContext context)
    {
        return context.UI.HasDialog;
    }

    public async Task<ActionResult> HandleAsync(IGameContext context, CancellationToken cancellationToken = default)
    {
        string? dialogText = context.UI.DialogText;
        
        // Đóng dialog
        context.UI.CloseDialog();
        await Task.Delay(150, cancellationToken);

        return ActionResult.Success($"Đã tự động đóng dialog: '{dialogText}'");
    }
}
