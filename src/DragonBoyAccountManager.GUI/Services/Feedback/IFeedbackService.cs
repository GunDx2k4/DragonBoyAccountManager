namespace DragonBoyAccountManager.GUI.Services.Feedback;

public interface IFeedbackService
{
    Task<FeedbackSendResult> SendFeedbackAsync(FeedbackReport report, CancellationToken cancellationToken = default);
}
