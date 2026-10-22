using System.Windows.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace DragonBoyAccountManager.GUI.Presentation.Features.Feedback;

public sealed partial class FeedbackViewModel : ObservableObject
{
    private readonly IFeedbackService _feedbackService;
    private readonly IDialogService _dialogService;
    private readonly string _appVersion;
    private readonly DispatcherTimer _cooldownTimer;

    public IReadOnlyList<FeedbackTypeOption> FeedbackTypes { get; } =
    [
        new(FeedbackType.FeatureRequest, "Góp ý tính năng mới / Cập nhật"),
        new(FeedbackType.BugReport, "Báo lỗi / Cần sửa lỗi"),
        new(FeedbackType.Optimization, "Đề xuất tối ưu / Hiệu năng"),
        new(FeedbackType.General, "Ý kiến & Góp ý khác")
    ];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendFeedbackCommand))]
    private FeedbackTypeOption _selectedTypeOption;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendFeedbackCommand))]
    private string _title = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendFeedbackCommand))]
    private string _content = string.Empty;

    [ObservableProperty]
    private string _senderName = string.Empty;

    [ObservableProperty]
    private string _contactInfo = string.Empty;

    [ObservableProperty]
    private bool _includeSystemInfo = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendFeedbackCommand))]
    [NotifyPropertyChangedFor(nameof(SubmitButtonText))]
    private bool _isSending;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private bool _isStatusSuccess;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendFeedbackCommand))]
    [NotifyPropertyChangedFor(nameof(SubmitButtonText))]
    private int _cooldownSeconds;

    public string SubmitButtonText =>
        IsSending ? "ĐANG GỬI..." :
        CooldownSeconds > 0 ? $"CHỜ ({CooldownSeconds}s)" :
        "GỬI GÓP Ý";

    public bool CanSend =>
        !IsSending &&
        CooldownSeconds <= 0 &&
        !string.IsNullOrWhiteSpace(Title) &&
        Title.Trim().Length >= 3 &&
        !string.IsNullOrWhiteSpace(Content) &&
        Content.Trim().Length >= 10;

    public FeedbackViewModel(
        IFeedbackService feedbackService,
        IDialogService dialogService)
    {
        _feedbackService = feedbackService;
        _dialogService = dialogService;

        _selectedTypeOption = FeedbackTypes[0];
        _appVersion = AppUpdaterService.CurrentVersionString;

        _cooldownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _cooldownTimer.Tick += CooldownTimer_Tick;
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendFeedbackAsync()
    {
        if (!CanSend) return;

        IsSending = true;
        StatusMessage = "Đang gửi ý kiến đóng góp đến Discord...";
        HasStatusMessage = true;
        IsStatusSuccess = true;

        try
        {
            var report = new FeedbackReport
            {
                Type = SelectedTypeOption.Type,
                Title = Title.Trim(),
                Content = Content.Trim(),
                SenderName = string.IsNullOrWhiteSpace(SenderName) ? null : SenderName.Trim(),
                ContactInfo = string.IsNullOrWhiteSpace(ContactInfo) ? null : ContactInfo.Trim(),
                IncludeSystemInfo = IncludeSystemInfo,
                AppVersion = _appVersion,
                OsVersion = Environment.OSVersion.VersionString,
                DotNetVersion = Environment.Version.ToString()
            };

            var result = await _feedbackService.SendFeedbackAsync(report);

            if (result.IsSuccess)
            {
                Title = string.Empty;
                Content = string.Empty;
                StatusMessage = "[OK] Đã gửi góp ý thành công! Cảm ơn bạn đã đóng góp phát triển DBAM.";
                IsStatusSuccess = true;

                _dialogService.ShowInfo(
                    "Góp ý thành công",
                    "Cảm ơn bạn! Ý kiến đóng góp / báo cáo lỗi của bạn đã được chuyển tới nhóm phát triển qua kênh Discord.");

                StartCooldown(20);
            }
            else
            {
                StatusMessage = $"Gửi thất bại: {result.ErrorMessage}";
                IsStatusSuccess = false;

                _dialogService.ShowError(
                    "Gửi góp ý thất bại",
                    $"Không thể gửi góp ý tới máy chủ:\n{result.ErrorMessage}\n\nVui lòng kiểm tra kết nối mạng và thử lại sau.");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi hệ thống: {ex.Message}";
            IsStatusSuccess = false;

            _dialogService.ShowError("Lỗi hệ thống", $"Đã xảy ra sự cố ngoài ý muốn: {ex.Message}");
        }
        finally
        {
            IsSending = false;
        }
    }

    [RelayCommand]
    private void ClearForm()
    {
        Title = string.Empty;
        Content = string.Empty;
        StatusMessage = string.Empty;
        HasStatusMessage = false;
    }

    private void StartCooldown(int seconds)
    {
        CooldownSeconds = seconds;
        _cooldownTimer.Start();
    }

    private void CooldownTimer_Tick(object? sender, EventArgs e)
    {
        if (CooldownSeconds > 1)
        {
            CooldownSeconds--;
        }
        else
        {
            CooldownSeconds = 0;
            _cooldownTimer.Stop();
        }
    }
}
