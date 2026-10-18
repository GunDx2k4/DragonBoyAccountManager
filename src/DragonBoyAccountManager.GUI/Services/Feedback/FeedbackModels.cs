namespace DragonBoyAccountManager.GUI.Services.Feedback;

public enum FeedbackType
{
    FeatureRequest,
    BugReport,
    Optimization,
    General
}

public sealed class FeedbackReport
{
    public FeedbackType Type { get; set; } = FeedbackType.FeatureRequest;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? SenderName { get; set; }
    public string? ContactInfo { get; set; }
    public bool IncludeSystemInfo { get; set; } = true;
    public string? AppVersion { get; set; }
    public string? OsVersion { get; set; }
    public string? DotNetVersion { get; set; }
}

public sealed record FeedbackSendResult(bool IsSuccess, string? ErrorMessage = null);
