using DragonBoyAccountManager.GUI.Services.Feedback;

namespace DragonBoyAccountManager.GUI.Common.Models;

public sealed record FeedbackTypeOption(FeedbackType Type, string DisplayName)
{
    public override string ToString() => DisplayName;
}
