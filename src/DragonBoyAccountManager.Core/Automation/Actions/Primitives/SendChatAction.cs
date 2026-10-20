
namespace DragonBoyAccountManager.Core.Automation.Actions.Primitives;

public class SendChatAction(string text, ChatType type = ChatType.Map) : GameAction($"Gửi chat ({type})")
{
    public string Text => text;
    public ChatType Type => type;

    protected override Task<ActionResult> OnExecuteAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Text))
        {
            return Task.FromResult(ActionResult.Failure("Nội dung chat trống."));
        }

        if (!context.IsRunning || !context.Player.IsInGame)
        {
            return Task.FromResult(ActionResult.Failure("Nhân vật chưa vào game."));
        }

        bool success = context.Network.SendChat(Text, Type);
        if (success)
        {
            progress?.Report($"[Chat] Đã gửi: {Text}");
            return Task.FromResult(ActionResult.Success($"Đã gửi: {Text}"));
        }

        return Task.FromResult(ActionResult.Failure("Không thể gửi tin nhắn chat."));
    }
}
