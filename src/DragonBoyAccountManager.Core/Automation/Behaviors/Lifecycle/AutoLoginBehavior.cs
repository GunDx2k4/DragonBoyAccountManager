using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Interop.Mono.Schema;
using DragonBoyAccountManager.Core.Sessions.Controllers;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Behaviors.Lifecycle;

public sealed class AutoLoginOptions
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public int ServerId { get; init; }
    public string? ServerName { get; init; }
    public int MaxAttempts { get; init; } = 10;
    public bool IsMasked { get; init; }
}

public sealed class AutoLoginBehavior : AutomationBehavior<AutoLoginOptions>
{
    private readonly LoginScreenController _loginScreen;
    private int _attempts;

    public int Attempts => _attempts;

    public AutoLoginBehavior(AutoLoginOptions options, LoginScreenController loginScreen)
        : base("Auto Login", options, "AutoLogin")
    {
        _loginScreen = loginScreen ?? throw new ArgumentNullException(nameof(loginScreen));
    }

    protected override void OnStarted(IProgress<string>? progress)
    {
        _attempts = 0;
        progress?.Report("[Auto Login] Bắt đầu quá trình đăng nhập...");
    }

    protected override async Task OnTickAsync(
        PlayerSnapshot player,
        WorldSnapshot world,
        MapSnapshot map,
        ICommandDispatcher dispatcher,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (player.IsInGame)
        {
            if (Options.IsMasked)
            {
                _loginScreen.ClearSensitiveInputs(Options.Username);
            }
            progress?.Report($"[Auto Login] Nhân vật đã vào game: {player.Name}");
            Stop();
            return;
        }

        if (_loginScreen.IsDownloadingData())
        {
            _loginScreen.TriggerDownloadData();

            while (_loginScreen.IsDownloadingData() && !cancellationToken.IsCancellationRequested)
            {
                int pct = _loginScreen.GetDownloadDataPercent();
                string pctText = pct > 0 ? $" ({pct}%)" : string.Empty;
                progress?.Report($"[Auto Login] Game đang tải dữ liệu{pctText}...");
                await Task.Delay(1000, cancellationToken);
            }

            progress?.Report("[Auto Login] Tải dữ liệu hoàn tất! Chờ game tự động chuyển giao diện...");

            var stabilizeDeadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < stabilizeDeadline && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(500, cancellationToken);

                if (!_loginScreen.IsLoadingControllerData() && !_loginScreen.IsUpdatingGameData())
                {
                    string? s = _loginScreen.GetCurrentScreenName();
                    if (s == GameSchema.ServerListScreen.ClassName || s == GameSchema.LoginScr.ClassName)
                    {
                        await Task.Delay(1000, cancellationToken);
                        break;
                    }
                }
            }

            progress?.Report("[Auto Login] Giao diện đã sẵn sàng, tiến hành đăng nhập...");
        }

        _attempts++;
        if (_attempts > Options.MaxAttempts)
        {
            progress?.Report($"[Auto Login] Đã thử đăng nhập {Options.MaxAttempts} lần không thành công.");
            Stop();
            return;
        }

        progress?.Report($"[Auto Login] [Lần {_attempts}/{Options.MaxAttempts}] Kiểm tra trạng thái game...");

        if (_loginScreen.HasActiveDialog(out bool isWait, out string? dlgText))
        {
            if (isWait)
            {
                progress?.Report($"[Auto Login] Chờ phản hồi: {dlgText ?? "Vui lòng chờ..."}");
                var waitDeadline = DateTime.UtcNow.AddSeconds(4);
                while (DateTime.UtcNow < waitDeadline && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(300, cancellationToken);
                    if (!_loginScreen.IsWaitDialogPresent())
                        break;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Stop();
                return;
            }

            if (_loginScreen.IsNonWaitDialogPresent())
            {
                _loginScreen.CloseDialog();
                await Task.Delay(400, cancellationToken);
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            Stop();
            return;
        }

        int targetServerId = _loginScreen.ResolveTargetServerId(Options.ServerId, Options.ServerName);
        string? curScreen = _loginScreen.GetCurrentScreenName();

        if (curScreen == GameSchema.SplashScr.ClassName || string.IsNullOrEmpty(curScreen))
        {
            progress?.Report("[Auto Login] Đang chờ game tải dữ liệu khởi động...");
            await Task.Delay(1000, cancellationToken);
            return;
        }

        if (curScreen == GameSchema.LoginScr.ClassName)
        {
            await HandleLoginScreenAsync(targetServerId, progress, cancellationToken);
        }
        else if (curScreen == GameSchema.ServerListScreen.ClassName)
        {
            _loginScreen.PreMaskServerListScreen(Options.IsMasked);

            if (!_loginScreen.IsServerListReady())
            {
                progress?.Report("[Auto Login] Chờ tải xong tài nguyên game trước khi tiếp tục...");
                await Task.Delay(1000, cancellationToken);
                return;
            }

            int curSelectedServer = _loginScreen.GetSelectedServerId();
            if (curSelectedServer >= 0 && curSelectedServer != targetServerId)
            {
                progress?.Report($"[Auto Login] Chọn máy chủ #{targetServerId}...");
                _loginScreen.SelectServer(targetServerId);
                await Task.Delay(500, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(Options.Username) && !string.IsNullOrWhiteSpace(Options.Password))
            {
                progress?.Report("[Auto Login] Mở màn hình đăng nhập...");
                _loginScreen.OpenLoginScreen(Options.IsMasked, Options.Username);

                var waitLoginScrDeadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < waitLoginScrDeadline && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(300, cancellationToken);
                    if (_loginScreen.GetCurrentScreenName() == GameSchema.LoginScr.ClassName)
                        break;
                }
                await Task.Delay(400, cancellationToken);

                if (_loginScreen.GetCurrentScreenName() == GameSchema.LoginScr.ClassName)
                {
                    await HandleLoginScreenAsync(targetServerId, progress, cancellationToken);
                }
            }
            else
            {
                progress?.Report("[Auto Login] Mở màn hình đăng nhập...");
                _loginScreen.LoginWithSavedCredentials();
                await Task.Delay(600, cancellationToken);
            }
        }
        else if (curScreen == GameSchema.SelectCharScr.ClassName)
        {
            progress?.Report("[Auto Login] Đang ở màn hình chọn nhân vật, tiến hành vào game...");
            _loginScreen.PerformScreenAction(GameSchema.SelectCharScr.ClassName, GameSchema.SelectCharScr.ActionSelect);
            await Task.Delay(800, cancellationToken);
        }
        else if (curScreen == GameSchema.ChooseCharScr.ClassName)
        {
            progress?.Report("[Auto Login] Đang ở màn hình chọn nhân vật, tiến hành vào game...");
            _loginScreen.PerformScreenAction(GameSchema.ChooseCharScr.ClassName, GameSchema.ChooseCharScr.ActionSelect);
            await Task.Delay(800, cancellationToken);
        }
        else
        {
            _loginScreen.SwitchToLoginScreen(Options.IsMasked, Options.Username);
            await Task.Delay(800, cancellationToken);
        }
    }

    private async Task HandleLoginScreenAsync(
        int targetServerId,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        int curSelectedServer = _loginScreen.GetSelectedServerId();
        if (curSelectedServer >= 0 && curSelectedServer != targetServerId)
        {
            progress?.Report("[Auto Login] Sai máy chủ, quay lại chọn máy chủ...");
            _loginScreen.CancelLogin();
            await Task.Delay(500, cancellationToken);
            return;
        }

        progress?.Report("[Auto Login] Điền thông tin tài khoản...");
        bool credsSet = false;
        for (int i = 0; i < 5 && !cancellationToken.IsCancellationRequested; i++)
        {
            if (_loginScreen.SetLoginCredentials(Options.Username, Options.Password, Options.IsMasked))
            {
                credsSet = true;
                break;
            }
            await Task.Delay(200, cancellationToken);
        }

        if (!credsSet)
        {
            progress?.Report("[Auto Login] Không thể điền tài khoản, thử lại...");
            await Task.Delay(500, cancellationToken);
            return;
        }

        await Task.Delay(200, cancellationToken);
        progress?.Report("[Auto Login] Gửi thông tin đăng nhập...");
        _loginScreen.SubmitLogin(Options.IsMasked, Options.Username, Options.Password);

        var loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < loginWaitTimeout && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(300, cancellationToken);

            if (_loginScreen.IsUpdatingGameData())
            {
                progress?.Report("[Auto Login] Máy chủ yêu cầu cập nhật dữ liệu (map, skill, item)... Đang tải...");
                var updateDeadline = DateTime.UtcNow.AddSeconds(90);
                while (_loginScreen.IsUpdatingGameData() && DateTime.UtcNow < updateDeadline && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, cancellationToken);
                }
                progress?.Report("[Auto Login] Cập nhật dữ liệu hoàn tất! Chờ ổn định...");
                await Task.Delay(1500, cancellationToken);
                loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
                continue;
            }

            if (_loginScreen.IsWaitingToLogin())
            {
                loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
                await Task.Delay(400, cancellationToken);
                continue;
            }

            string? checkScreen = _loginScreen.GetCurrentScreenName();
            if (checkScreen == GameSchema.GameScr.ClassName)
            {
                Stop();
                return;
            }

            if (checkScreen == GameSchema.SelectCharScr.ClassName)
            {
                progress?.Report("[Auto Login] Chọn nhân vật vào game...");
                await Task.Delay(500, cancellationToken);
                _loginScreen.PerformScreenAction(GameSchema.SelectCharScr.ClassName, GameSchema.SelectCharScr.ActionSelect);
                loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
                continue;
            }

            if (checkScreen == GameSchema.ChooseCharScr.ClassName)
            {
                progress?.Report("[Auto Login] Chọn nhân vật vào game...");
                await Task.Delay(500, cancellationToken);
                _loginScreen.PerformScreenAction(GameSchema.ChooseCharScr.ClassName, GameSchema.ChooseCharScr.ActionSelect);
                loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
                continue;
            }

            if (_loginScreen.IsNonWaitDialogPresent())
                break;

            if (checkScreen == GameSchema.ServerListScreen.ClassName)
            {
                if (_loginScreen.IsDownloadingData())
                {
                    progress?.Report("[Auto Login] Máy chủ yêu cầu tải dữ liệu...");
                    var dlDeadline = DateTime.UtcNow.AddSeconds(90);
                    while (_loginScreen.IsDownloadingData() && DateTime.UtcNow < dlDeadline && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                    await Task.Delay(1500, cancellationToken);
                }

                await Task.Delay(300, cancellationToken);
                progress?.Report("[Auto Login] Xác thực hoàn tất, đang vào game...");
                _loginScreen.LoginWithSavedCredentials();
                loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
            }
        }
    }
}
