namespace DragonBoyAccountManager.GUI.Domain.Feedback;

public sealed record FeedbackTypeOption(FeedbackType Type, string DisplayName)
{
    public override string ToString() => DisplayName;
}
