
namespace DragonBoyAccountManager.Core.Automation.Interrupters;

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

        context.UI.CloseDialog();
        await Task.Delay(150, cancellationToken);

        return ActionResult.Success($"Đã tự động đóng hộp thoại: '{dialogText}'");
    }
}
