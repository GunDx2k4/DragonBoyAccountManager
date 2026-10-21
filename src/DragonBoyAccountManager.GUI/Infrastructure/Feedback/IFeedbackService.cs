namespace DragonBoyAccountManager.GUI.Infrastructure.Feedback;

public interface IFeedbackService
{
    bool IsConfigured { get; }
    Task<FeedbackSendResult> SendFeedbackAsync(FeedbackReport report, CancellationToken cancellationToken = default);
}
